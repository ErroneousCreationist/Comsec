using System;
using Raylib_cs;

namespace comsec
{
    /// <summary>
    /// A custom IO terminal, dislays messages newest lowest and has more control over the input bar, which also is independent of output.
    /// </summary>
	public class CustomTerminal
	{
        private int WIDTH, HEIGHT;
        private string WINDOWTEXT;
        private List<string> MESSAGES;
        private string CurrText;
        private bool Inputed = false;
        private bool RecievingInput = false;
        private int MaxLength;
        private bool KeyInput;
        public bool INIT;
        public static CustomTerminal? TERMINAL; //there can only be one so this is fine
        private Color BACKGROUND_COLOUR, TEXT_COLOUR, INPUT_COLOUR;

        #region Helper funcs
        private static bool GetKeyDown(KeyboardKey key)
        {
            return Raylib.IsKeyPressed(key);
        }

        private static bool GetKey(KeyboardKey key)
        {
            return Raylib.IsKeyDown(key);
        }

        private static bool GetKeyUp(KeyboardKey key)
        {
            return Raylib.IsKeyReleased(key);
        }

        private static List<char> GetCharsPressed
        {
            get
            {
                List<char> returned = new List<char>();
                while (true)
                {
                    int i = Raylib.GetCharPressed();
                    if (i == 0) { break; }
                    returned.Add((char)i);
                }
                return returned;
            }
        }
        #endregion

        /// <summary>
        /// Init custom terminal with default keyboard parameters, opening the window immediately
        /// </summary>
        /// <param name="width">The width of the window</param>
        /// <param name="height">The height of the window</param>
        /// <param name="windowText">The text in the top of the window</param>
        public CustomTerminal(int width, int height, string windowText)
		{
            TERMINAL = this;
            WIDTH = width;
            HEIGHT = height;
            MESSAGES = new List<string>();
            CurrText = "";
            RecievingInput = false;
            Inputed = false;
            WINDOWTEXT = windowText;
            MaxLength = 100;
            KeyInput = false;
            BACKGROUND_COLOUR = Color.White;
            TEXT_COLOUR = Color.Black;
            INPUT_COLOUR = Color.LightGray; //input colour
            WindowUpdateThread();

        }

        private void WindowUpdateThread()
        {
            INIT = false;
            //open the raylib window
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.TransparentWindow);
            Raylib.InitWindow(WIDTH, HEIGHT, WINDOWTEXT);
            INIT = true;

            int scrolloffset = 0;
            float HoldDeleteFrames = 1000, DeleteSpeed = 80, TypeCooldown = 20;
            //private int currlines;
            float t = 0, e = 0, x = 0, flashcd = 1000;
            bool cursor = false;
            int cursorPos = 0; //distance from the end of the string

            while (!Raylib.WindowShouldClose())
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(BACKGROUND_COLOUR);

                WIDTH = Raylib.GetScreenWidth();
                HEIGHT = Raylib.GetScreenHeight();

                var scroll = Raylib.GetMouseWheelMoveV().Y*4;
                if (scrolloffset < 0) { scrolloffset = 0; }
                if((HEIGHT - 50 - (20*(MESSAGES.Count+1)) + scrolloffset + (int) scroll < 0)) { scrolloffset += (int)scroll; }
                for (int i = 0; i < MESSAGES.Count; i++)
                {
                    Raylib.DrawText(MESSAGES[i], 10, HEIGHT - 50 - (20 * (i + 1)) + scrolloffset, 20, TEXT_COLOUR);
                }

                #region Input field
                if (GetKeyDown(KeyboardKey.Left)) { cursor = true; flashcd = 500; cursorPos++; if (cursorPos > CurrText.Length) { cursorPos = 0; } }
                if (GetKeyDown(KeyboardKey.Right)) { cursor = true; flashcd = 500; cursorPos--; if (cursorPos < 0) { cursorPos = CurrText.Length; } }

                List<char> stuff = Inputed || !RecievingInput ? new() : GetCharsPressed; //pause input while waiting for return just in case, and also pause if we arent waiting for input
                if(!GetKey(KeyboardKey.LeftControl) && !GetKey(KeyboardKey.RightControl))
                {
                    if (stuff.Count > 0 && x <= 0 && CurrText.Length <= MaxLength) { x = TypeCooldown; CurrText = CurrText.Insert(CurrText.Length - cursorPos, new string(stuff.ToArray())); }
                }
                else
                {
                    if(GetKeyDown(KeyboardKey.V)) { x = TypeCooldown; CurrText = CurrText.Insert(CurrText.Length - cursorPos, Raylib.GetClipboardText_()); }
                }
                if(CurrText.Length>=1 && KeyInput) { Inputed = true; cursorPos = 0; }
                x--;
                if (!Inputed && cursorPos!=CurrText.Length && (GetKeyDown(KeyboardKey.Backspace) || GetKeyDown(KeyboardKey.Delete)) && CurrText.Length > 0) { if (cursorPos == 0) { CurrText = CurrText[..^1]; } else { CurrText = CurrText.Remove(CurrText.Length - 1 - cursorPos, 1); } }
                if (!Inputed && cursorPos != CurrText.Length &&(GetKey(KeyboardKey.Backspace) || GetKey(KeyboardKey.Delete))) { t++; if (t >= HoldDeleteFrames && CurrText.Length > 0) { e--; if (e <= 0) { e = DeleteSpeed; if (cursorPos == 0) { CurrText = CurrText[..^1]; } else { CurrText = CurrText.Remove(CurrText.Length - 1 - cursorPos, 1); } } } }
                else { t = 0; }
                if (cursorPos > CurrText.Length) { cursorPos = CurrText.Length; }
                flashcd--;
                if (flashcd <= 0) { flashcd = 1000; cursor = !cursor; }
                if (RecievingInput)
                {
                    Raylib.DrawRectangle(0, HEIGHT - 30, WIDTH, 30, INPUT_COLOUR);
                    string textbeforecursor = CurrText[..^cursorPos];
                    if (cursor) { Raylib.DrawRectangle(cursorPos == CurrText.Length ? 0 : Raylib.MeasureText(textbeforecursor, 30) + (Raylib.MeasureText(textbeforecursor[^1].ToString(), 30)), HEIGHT - 30, 5, 30, new Color((int)(INPUT_COLOUR.R * 0.5f), (int)(INPUT_COLOUR.G * 0.5f), (int)(INPUT_COLOUR.B * 0.5f), INPUT_COLOUR.A)); }
                    Raylib.DrawText(CurrText, 10, HEIGHT - 30, 30, TEXT_COLOUR);
                }
                if ((GetKeyDown(KeyboardKey.Enter) || GetKeyDown(KeyboardKey.KpEnter)) && !string.IsNullOrWhiteSpace(CurrText) && !KeyInput)
                {
                    Inputed = true;
                    cursorPos = 0;
                }
                #endregion

                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();
            Environment.Exit(0);
        }

        /// <summary>
        /// Call this to close terminal and exit the application
        /// </summary>
        public void Close()
        {
            Raylib.CloseWindow();
            Environment.Exit(0);
        }

        /// <summary>
        /// Print message to terminal (has a newline anyway because of how the text works)
        /// </summary>
        /// <param name="text">The text to write</param>
        public void Output(string text)
        {
            MESSAGES.Insert(0, text);
        }

        /// <summary>
        /// Waits for input from the terminal (pauses current thread)
        /// </summary>
        /// <returns>text from the input</returns>
        public string Input(int maxlength = 100)
        {
            MaxLength = maxlength;
            RecievingInput = true;
            while(!Inputed)
            {
                Thread.Sleep(1);
            }
            string input = CurrText;
            CurrText = "";
            RecievingInput = false;
            Inputed = false;
            return input;
        }

        /// <summary>
        /// Waits for a single key input from the terminal (pauses current thread)
        /// </summary>
        /// <returns>char that was inputed</returns>
        public char InputKey()
        {
            MaxLength = 1;
            RecievingInput = true;
            KeyInput = true;
            while (!Inputed)
            {
                Thread.Sleep(1);
            }
            string input = CurrText;
            KeyInput = false;
            CurrText = "";
            RecievingInput = false;
            Inputed = false;
            return input[0];
        }

        /// <summary>
        /// Clears the terminal messages
        /// </summary>
        public void Clear()
        {
            MESSAGES = new List<string>(); //clear terminal
        }

        /// <summary>
        /// Sets the background colour, opacity supported.
        /// </summary>
        /// <param name="col">The colour to set it to</param>
        public void SetBackgroundColour(Color col)
        {
            BACKGROUND_COLOUR = col;
            if (col.A > 0 && col.A <= 255) { Raylib.SetWindowOpacity(col.A / 255f); }
            else { Raylib.SetWindowOpacity(255f); }
        }

        /// <summary>
        /// Sets the colour of all text, including the input field text, opacity supported.
        /// </summary>
        /// <param name="col">The colour to set it to</param>
        public void SetTextColour(Color col)
        {
            TEXT_COLOUR = col;
        }

        /// <summary>
        /// Sets the colour of the input field and the cursor (which is set to 2x darker than this colour), opacity supported.
        /// </summary>
        /// <param name="col">The colour to set it to</param>
        public void SetInputColour(Color col)
        {
            INPUT_COLOUR = col;
        }
    }
}

