using System;
using System.Text;
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
        public static Dictionary<string,Texture2D>? EMOJIS;

        private Action? WINDOW_CLOSED;

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
                List<char> returned = new();
                while (true)
                {
                    int i = Raylib.GetCharPressed();
                    if (i == 0) { break; }
                    //Console.WriteLine((char)i);
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
        public CustomTerminal(int width, int height, string windowText, Action onwindowclose)
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
            WINDOW_CLOSED = onwindowclose;
            WindowUpdateThread();

        }

        private void WindowUpdateThread()
        {
            INIT = false;
            //open the raylib window
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.TransparentWindow);
            Raylib.InitWindow(WIDTH, HEIGHT, WINDOWTEXT);
            INIT = true;

            //set the working directory correctly on mac, yes we have to use pointers im sorry goofy ahh stupid C integration thibng ffs
            if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                string str = AppContext.BaseDirectory;
                byte[] bytes = Encoding.ASCII.GetBytes(str);
                unsafe
                {
                    fixed (byte* p = bytes)
                    {
                        sbyte* sp = (sbyte*)p;
                        //SP is now what you want
                        Raylib.ChangeDirectory(sp);
                    }
                }
            }

            Font font = Raylib.GetFontDefault();
            if (!Directory.Exists(AppContext.BaseDirectory + "/resources")) { EMOJIS = new(); Console.WriteLine("Uh oh, resources directory is missing, thats not good."); }
            else
            {
                if (!File.Exists(AppContext.BaseDirectory + "/resources/font.ttf")) { Console.WriteLine("No font found (resources/font.ttf), using fallback."); }
                else { font = Raylib.LoadFont("resources/font.ttf"); }

                if (!Directory.Exists(AppContext.BaseDirectory + "/resources/emojis")) { EMOJIS = new(); Console.WriteLine("Uh oh, emojis directory is missing, thats not good."); }
                else
                {
                    EMOJIS = new();
                    foreach (var item in Directory.EnumerateFiles(AppContext.BaseDirectory + "/resources/emojis"))
                    {
                        //Console.WriteLine(item + " \\ "+ )
                        EMOJIS.Add(Path.GetFileName(item).Split(".")[0].ToLower().Replace(" ","").Replace("-",""), Raylib.LoadTexture("resources/emojis/" + Path.GetFileName(item))); //add the emoji name to the list
                    }
                }
            }

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
                    var ypos = HEIGHT - 50 - (20 * (i + 1)) + scrolloffset;
                    if(ypos < 0) { return; }
                    //its an emoji, render it as such
                    if (MESSAGES[i].Split(' ').Length==3 && MESSAGES[i].Split(' ')[1] == "/emoji" && EMOJIS.ContainsKey(MESSAGES[i].Split(' ')[2].ToLower()))
                    {
                        string str = MESSAGES[i].Split(' ')[0];
                        Raylib.DrawTextEx(font, str, new System.Numerics.Vector2(10, ypos), 20, 1, TEXT_COLOUR);
                        var width = (int)Raylib.MeasureTextEx(font, str, 30, 1).X;

                        var tex = EMOJIS[MESSAGES[i].Split(' ')[2].ToLower()];
                        Raylib.DrawTextureEx(tex, new System.Numerics.Vector2(width-15, ypos), 0, 20f / tex.Height, Color.White);
                    }
                    else { Raylib.DrawTextEx(font, MESSAGES[i], new System.Numerics.Vector2(10, ypos), 20, 1, TEXT_COLOUR); }
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
                if(!Inputed && GetKey(KeyboardKey.LeftControl) && (GetKeyDown(KeyboardKey.Backspace) || GetKeyDown(KeyboardKey.Delete))) { CurrText = ""; cursorPos = 0; e = DeleteSpeed; } //delete eveerything
                if (!Inputed && cursorPos != CurrText.Length &&(GetKey(KeyboardKey.Backspace) || GetKey(KeyboardKey.Delete))) { t++; if (t >= HoldDeleteFrames && CurrText.Length > 0) { e--; if (e <= 0) { e = DeleteSpeed; if (cursorPos == 0) { CurrText = CurrText[..^1]; } else { CurrText = CurrText.Remove(CurrText.Length - 1 - cursorPos, 1); } } } }
                else { t = 0; }
                if (cursorPos > CurrText.Length) { cursorPos = CurrText.Length; }
                flashcd--;
                if (flashcd <= 0) { flashcd = 1000; cursor = !cursor; }
                if (RecievingInput)
                {
                    Raylib.DrawRectangle(0, HEIGHT - 30, WIDTH, 30, INPUT_COLOUR);
                    string textbeforecursor = CurrText[..^cursorPos];
                    if (cursor) { Raylib.DrawRectangle(cursorPos == CurrText.Length ? 0 : (int)Raylib.MeasureTextEx(font, textbeforecursor, 30, 1).X + (int)Raylib.MeasureTextEx(font, textbeforecursor[^1].ToString(), 30, 1).X/2, HEIGHT - 30, 5, 30, new Color((int)(INPUT_COLOUR.R * 0.5f), (int)(INPUT_COLOUR.G * 0.5f), (int)(INPUT_COLOUR.B * 0.5f), INPUT_COLOUR.A)); }
                    Raylib.DrawTextEx(font, CurrText, new System.Numerics.Vector2(10, HEIGHT - 30), 30, 1, TEXT_COLOUR);
                }
                if ((GetKeyDown(KeyboardKey.Enter) || GetKeyDown(KeyboardKey.KpEnter)) && !string.IsNullOrWhiteSpace(CurrText))
                {
                    if (KeyInput) { CurrText = "\n"; }
                    Inputed = true;
                    cursorPos = 0;
                }
                #endregion

                Raylib.EndDrawing();
            }
            WINDOW_CLOSED?.Invoke();

            Raylib.CloseWindow();
            Environment.Exit(0);
        }

        /// <summary>
        /// Call this to close terminal and exit the application
        /// </summary>
        public void Close()
        {
            Raylib.EndDrawing();
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
            try
            {
                if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    Console.WriteLine("Maybe fix the memory issue maybe!");
                }
                else
                {
                    Raylib.SetWindowOpacity(col.A < 100 ? 100 / 255f : col.A);
                }
            }
            catch
            {
                Console.WriteLine("Window opacity screwed up??");
            }
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

