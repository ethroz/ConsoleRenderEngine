using SharpDX;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleExtender
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ConsoleFont
    {
        public uint Index;
        public short SizeX, SizeY;
    }

    public static class ConsoleHelper
    {
        [DllImport("kernel32")]
        private static extern bool SetConsoleIcon(IntPtr hIcon);

        public static bool SetConsoleIcon(Icon icon)
        {
            return SetConsoleIcon(icon.Handle);
        }

        [DllImport("kernel32")]
        private extern static bool SetConsoleFont(IntPtr hOutput, uint index);

        private enum StdHandle
        {
            OutputHandle = -11
        }

        [DllImport("kernel32")]
        private static extern IntPtr GetStdHandle(StdHandle index);

        public static bool SetConsoleFont(uint index)
        {
            return SetConsoleFont(GetStdHandle(StdHandle.OutputHandle), index);
        }

        [DllImport("kernel32")]
        private static extern bool GetConsoleFontInfo(IntPtr hOutput, [MarshalAs(UnmanagedType.Bool)] bool bMaximize,
            uint count, [MarshalAs(UnmanagedType.LPArray), Out] ConsoleFont[] fonts);

        [DllImport("kernel32")]
        private static extern uint GetNumberOfConsoleFonts();

        public static uint ConsoleFontsCount
        {
            get
            {
                return GetNumberOfConsoleFonts();
            }
        }

        public static ConsoleFont[] ConsoleFonts
        {
            get
            {
                ConsoleFont[] fonts = new ConsoleFont[GetNumberOfConsoleFonts()];
                if (fonts.Length > 0)
                    GetConsoleFontInfo(GetStdHandle(StdHandle.OutputHandle), false, (uint)fonts.Length, fonts);
                return fonts;
            }
        }

    }
}

namespace ConsoleRenderEngine
{
    class Gameobject
    {
        public string name;
        public Vector3 position, rotation, scale;
        public Triangle[] triangles;

        public Gameobject()
        {
            name = "object";
            position = new Vector3();
            rotation = new Vector3();
            scale = new Vector3(1.0f);
        }

        public Gameobject(string name)
        {
            this.name = name;
            position = new Vector3();
            rotation = new Vector3();
            scale = new Vector3(1.0f);
        }

        public Gameobject(string name, Vector3 p, Vector3 r, Vector3 s, Triangle[] t)
        {
            position = p;
            rotation = r;
            scale = s;
            triangles = t;
        }
    }

    class Triangle
    {
        public Vector4[] v = new Vector4[3];
        public char col;

        public Triangle()
        {
            v[0] = new Vector4();
            v[1] = new Vector4();
            v[2] = new Vector4();
            col = '█';
        }

        public Triangle(Vector4 a, Vector4 b, Vector4 c)
        {
            v[0] = a;
            v[1] = b;
            v[2] = c;
            col = '█';
        }

        public Triangle(Vector4 a, Vector4 b, Vector4 c, char col)
        {
            v[0] = a;
            v[1] = b;
            v[2] = c;
            this.col = col;
        }
    }

    class Chey
    {
        public Key key;
        public bool Down, Up, Held, Raised;

        public Chey(Key key)
        {
            this.key = key;
            Down = Up = Held = false;
            Raised = true;
        }
    }

    class Button
    {
        public bool Down, Up, Held, Raised;

        public Button()
        {
            Down = Up = Held = false;
            Raised = true;
        }
    }

    class Program
    {
        // engine fields
        const int MF_BYCOMMAND = 0x00000000;
        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        const int SC_SIZE = 0xF000;
        const char FULL_BLOCK = '█';
        const char THREE_QUARTER_BLOCK = '▓';
        const char HALF_BLOCK = '▒';
        const char QUARTER_BLOCK = '░';
        const char EMPTY_BLOCK = ' ';
        const float Deg2Rad = (float)Math.PI / 180.0f;
        static char[] display;
        static float[] depthBuffer;
        static Stopwatch sw;
        static Stopwatch sw2;
        static Mouse mouse;
        static Button[] buttons;
        static Vector2 DeltaMousePos;
        static int DeltaMouseScroll;
        static Keyboard keyboard;
        static Chey[] cheyArray;
        static int Width = 0, Height = 0;
        static double FrameRate = 0.0;
        static double PollingRate = 1000.0;
        static double elapsedTime;
        static long t1, t2;
        static bool debug = false;
        static string debugText = "";

        // specified fields
        static float MoveSpeed = 4.0f;
        static float Sensitivity = 0.042f;
        static Vector3 EyePos = new Vector3();
        static Vector2 EyeRot = new Vector2();
        static Vector3 LightDirection = new Vector3(1.0f, -1.0f, 1.0f);
        static List<Gameobject> gameobjects;
        static Matrix ProjectionMatrix;
        static float theta = 0.0f;

        static void Main()
        {
            // console manipulation
            Console.Title = "ConsoleRenderEngine";
            Console.CursorVisible = false;
            Console.Clear();
            if (Width == 0)
                Width = Console.LargestWindowWidth;
            if (Height == 0)
                Height = Console.LargestWindowHeight - 1;
            if (Height > Console.LargestWindowHeight - 1)
            {
                Width = (int)((float)Width * (Console.LargestWindowHeight - 1));
                Height = Console.LargestWindowHeight - 1;
            }
            Console.SetWindowSize(Width, Height + 1);
            Console.SetBufferSize(Width, Height + 1);
            //DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_MINIMIZE, MF_BYCOMMAND);
            DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_MAXIMIZE, MF_BYCOMMAND);
            DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_SIZE, MF_BYCOMMAND);

            // field inititalization
            display = new char[Width * Height];
            depthBuffer = new float[Width * Height];
            Clear();
            sw = new Stopwatch();
            sw2 = new Stopwatch();
            t1 = t2 = 0;
            gameobjects = new List<Gameobject>();

            // starting function calls
            InitializeMouse();
            InitializeKeyboard();
            OnStart();
            sw.Start();
            Thread t = new Thread(() => UserInput());
            t.Start();

            // render loop
            while (true)
            {
                // force framerate
                t2 = sw.ElapsedTicks;
                elapsedTime = (t2 - t1) / 10000000.0;
                double before = elapsedTime;
                if (FrameRate != 0.0)
                {
                    while (1.0 / elapsedTime > FrameRate)
                    {
                        t2 = sw.ElapsedTicks;
                        elapsedTime = (t2 - t1) / 10000000.0;
                    }
                }
                double after = elapsedTime;
                t1 = t2;

                OnUpdate();

                // end of frame
                Render();
                if (debug)
                {
                    Console.SetCursorPosition(0, Height);
                    if (FrameRate != 0.0)
                    {
                        Console.Write("Free Time: " + ((after - before) * FrameRate * 100) + "% " + debugText);
                    }
                    else
                    {
                        Console.Write(debugText);
                    }
                    Console.Title = "ConsoleRenderEngine   FPS:" + (1.0 / (elapsedTime)).ToString("G4");
                }
            }
        }

        static void OnStart()
        {
            Triangle[] tris = new Triangle[1]
            {
                new Triangle(new Vector4(-0.05f, 0.0f, 0.1f, 1.0f), new Vector4(0.0f, 0.15f, 0.6f, 1.0f), new Vector4(0.6f, -0.3f, 1.1f, 1.0f))
            };
            gameobjects.Add(new Gameobject("triangle", new Vector3(), new Vector3(), new Vector3(1.0f), tris));

            //Triangle[] tris = new Triangle[12]
            //{
            //    new Triangle(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(-1.0f, 1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, -1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, -1.0f, 1.0f), new Vector4(1.0f, -1.0f, -1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, 1.0f, 1.0f), new Vector4(1.0f, -1.0f, 1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, 1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(-1.0f, 1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, -1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, -1.0f, 1.0f, 1.0f), new Vector4(-1.0f, -1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, 1.0f, -1.0f, 1.0f), new Vector4(-1.0f, 1.0f, 1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, 1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(1.0f, 1.0f, -1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(-1.0f, -1.0f, 1.0f, 1.0f), new Vector4(-1.0f, 1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(-1.0f, 1.0f, 1.0f, 1.0f), new Vector4(-1.0f, 1.0f, -1.0f, 1.0f)),
            //    new Triangle(new Vector4(1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(1.0f, -1.0f, 1.0f, 1.0f))
            //};
            //gameobjects.Add(new Gameobject("cube", new Vector3(0.0f, 0.0f, 3.0f), new Vector3(), new Vector3(0.5f), tris));
            //tris = new Triangle[2]
            //{
            //    new Triangle(new Vector4(-1.0f, 0.0f, -1.0f, 1.0f), new Vector4(-1.0f, 0.0f, 1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
            //    new Triangle(new Vector4(-1.0f, 0.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector4(1.0f, 0.0f, -1.0f, 1.0f)),
            //};
            //gameobjects.Add(new Gameobject("plane", new Vector3(0.0f, -1.0f, 0.0f), new Vector3(), new Vector3(100.0f), tris));

            ProjectionMatrix = Matrix.PerspectiveFovLH(59.0f * Deg2Rad, (float)Width / Height, 0.1f, 1000.0f);
            LightDirection = Vector3.Normalize(LightDirection);
        }

        static void OnUpdate()
        {
            Clear();
            Matrix cameraRot = Matrix.RotationX(EyeRot.X * Deg2Rad) * Matrix.RotationY(EyeRot.Y * Deg2Rad);
            Vector3 vForwards = EyePos + Vector3.TransformNormal(Vector3.ForwardLH, cameraRot);
            Vector3 vUpwards = Vector3.TransformNormal(Vector3.Up, cameraRot);
            Matrix view = Matrix.LookAtLH(EyePos, vForwards, vUpwards);
            theta += (float)elapsedTime / Deg2Rad;
            //gameobjects[0].rotation = new Vector3(0.5f * theta, 0.0f, theta);
            for (int i = 0; i < gameobjects.Count; i++)
            {
                // vertex shader and rasterization
                Matrix scale = Matrix.Scaling(gameobjects[i].scale);
                Matrix rotation = Matrix.RotationZ(gameobjects[i].rotation.Z * Deg2Rad) * Matrix.RotationY(gameobjects[i].rotation.Y * Deg2Rad) * Matrix.RotationX(gameobjects[i].rotation.X * Deg2Rad);
                Matrix translation = Matrix.Translation(gameobjects[i].position);
                Matrix world = scale * rotation * translation;
                List<Triangle> trisToRaster = new List<Triangle>();
                foreach (Triangle t in gameobjects[i].triangles)
                {
                    Triangle tp = new Triangle();
                    tp.v[0] = Vector4.Transform(t.v[0], world);
                    tp.v[1] = Vector4.Transform(t.v[1], world);
                    tp.v[2] = Vector4.Transform(t.v[2], world);

                    Vector3 normal = Vector3.Normalize(Vector3.Cross((Vector3)(tp.v[1] - tp.v[0]), (Vector3)(tp.v[2] - tp.v[0])));

                    if (Vector3.Dot(normal, (Vector3)tp.v[0] - EyePos) < 0.0f)
                    {
                        tp.v[0] = Vector4.Transform(tp.v[0], view);
                        tp.v[1] = Vector4.Transform(tp.v[1], view);
                        tp.v[2] = Vector4.Transform(tp.v[2], view);

                        char col = GetColor(-Vector3.Dot(normal, LightDirection));

                        Triangle[] clipped = new Triangle[2];
                        int nClippedTriangles = TriangleClipAgainstPlane(new Vector3(0.0f, 0.0f, 0.1f), new Vector3(0.0f, 0.0f, 1.0f), ref tp, out clipped[0], out clipped[1]);

                        for (int j = 0; j < nClippedTriangles; j++)
                        {
                            Triangle c = new Triangle();
                            c.v[0] = Vector4.Transform(clipped[j].v[0], ProjectionMatrix);
                            c.v[1] = Vector4.Transform(clipped[j].v[1], ProjectionMatrix);
                            c.v[2] = Vector4.Transform(clipped[j].v[2], ProjectionMatrix);
                            //if (c.v[0].W != 0.0f)
                            //{
                            //    c.v[0].X /= c.v[0].W; c.v[0].Y /= c.v[0].W; c.v[0].Z /= c.v[0].W;
                            //}
                            //if (c.v[1].W != 0.0f)
                            //{
                            //    c.v[1].X /= c.v[1].W; c.v[1].Y /= c.v[1].W; c.v[1].Z /= c.v[1].W;
                            //}
                            //if (c.v[2].W != 0.0f)
                            //{
                            //    c.v[2].X /= c.v[2].W; c.v[2].Y /= c.v[2].W; c.v[2].Z /= c.v[2].W;
                            //}

                            if (c.v[0].W != 0.0f)
                            {
                                c.v[0].X /= c.v[0].W; c.v[0].Y /= c.v[0].W;
                            }
                            if (c.v[1].W != 0.0f)
                            {
                                c.v[1].X /= c.v[1].W; c.v[1].Y /= c.v[1].W;
                            }
                            if (c.v[2].W != 0.0f)
                            {
                                c.v[2].X /= c.v[2].W; c.v[2].Y /= c.v[2].W;
                            }
                            c.v[0].Z = c.v[0].W;
                            c.v[1].Z = c.v[1].W;
                            c.v[2].Z = c.v[2].W;

                            // normalized ss to console ss
                            c.v[0].X += 1.0f; c.v[0].Y = -c.v[0].Y + 1.0f;
                            c.v[1].X += 1.0f; c.v[1].Y = -c.v[1].Y + 1.0f;
                            c.v[2].X += 1.0f; c.v[2].Y = -c.v[2].Y + 1.0f;

                            c.v[0].X *= 0.5f * Width;
                            c.v[0].Y *= 0.5f * Height;
                            c.v[1].X *= 0.5f * Width;
                            c.v[1].Y *= 0.5f * Height;
                            c.v[2].X *= 0.5f * Width;
                            c.v[2].Y *= 0.5f * Height;

                            Triangle[] edged = new Triangle[2];
                            List<Triangle> listTriangles = new List<Triangle> { c };
                            int nNewTriangles = 1;

                            for (int p = 0; p < 4; p++)
                            {
                                while (nNewTriangles > 0)
                                {
                                    int nTrisToAdd;
                                    Triangle test = listTriangles[0];
                                    listTriangles.RemoveAt(0);
                                    nNewTriangles--;

                                    switch (p)
                                    {
                                        case 0:
                                            nTrisToAdd = TriangleClipAgainstPlane(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                            break;
                                        case 1:
                                            nTrisToAdd = TriangleClipAgainstPlane(new Vector3(0.0f, Height - 1, 0.0f), new Vector3(0.0f, -1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                            break;
                                        case 2:
                                            nTrisToAdd = TriangleClipAgainstPlane(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                            break;
                                        default:
                                            nTrisToAdd = TriangleClipAgainstPlane(new Vector3(Width - 1, 0.0f, 0.0f), new Vector3(-1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                            break;
                                    }

                                    for (int w = 0; w < nTrisToAdd; w++)
                                        listTriangles.Add(new Triangle(edged[w].v[0], edged[w].v[1], edged[w].v[2], col));
                                }
                                nNewTriangles = listTriangles.Count;
                            }
                            for (int k = 0; k < listTriangles.Count; k++)
                                trisToRaster.Add(listTriangles[k]);
                        }
                    }
                }

                // pixel shader
                Parallel.ForEach(trisToRaster, (Triangle t) =>
                {
                    FillTriangle((int)t.v[0].X, (int)t.v[0].Y, t.v[0].Z, (int)t.v[1].X, (int)t.v[1].Y, t.v[1].Z, (int)t.v[2].X, (int)t.v[2].Y, t.v[2].Z, t.col);
                    //DrawTriangle((int)t.v[0].X, (int)t.v[0].Y, (int)t.v[1].X, (int)t.v[1].Y, (int)t.v[2].X, (int)t.v[2].Y, EMPTY_BLOCK);
                });
            }
        }

        static void UserInput()
        {
            sw2.Start();
            long t1 = 0, t2;
            double elapsedTime;
            while (true)
            {
                t2 = sw2.ElapsedTicks;
                elapsedTime = (t2 - t1) / 10000000.0;
                if (PollingRate != 0.0)
                {
                    while (1.0 / elapsedTime > PollingRate)
                    {
                        t2 = sw2.ElapsedTicks;
                        elapsedTime = (t2 - t1) / 10000000.0;
                    }
                }
                t1 = t2;
                GetMouseData();
                GetKeys();
                //////////////////////////

                if (KeyDown(Key.Escape))
                    Environment.Exit(0);
                if (KeyHeld(Key.LeftShift) && KeyDown(Key.Grave))
                    debug = !debug;
                if (KeyHeld(Key.LeftShift))
                    MoveSpeed *= 2.0f;
                EyeRot.Y += DeltaMousePos.X * Sensitivity;
                EyeRot.X += DeltaMousePos.Y * Sensitivity;
                EyeRot.X = Math.Max(Math.Min(EyeRot.X, 90.0f), -90.0f);
                Matrix roty = Matrix.RotationY(EyeRot.Y * Deg2Rad);
                Matrix rotx = Matrix.RotationX(EyeRot.X * Deg2Rad);
                Matrix rot = rotx * roty;
                float normalizer = Math.Max((float)Math.Sqrt((KeyHeld(Key.A) ^ KeyHeld(Key.D) ? 1 : 0) + (KeyHeld(Key.W) ^ KeyHeld(Key.S) ? 1 : 0) + (KeyHeld(Key.E) ^ KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
                Vector3 forward = Vector3.TransformNormal(Vector3.ForwardLH, rot) / normalizer;
                Vector3 right = Vector3.TransformNormal(Vector3.Right, rot) / normalizer;
                Vector3 up = Vector3.TransformNormal(Vector3.Up, rot) / normalizer;
                if (KeyHeld(Key.A))
                    EyePos -= right * (float)elapsedTime * MoveSpeed;
                if (KeyHeld(Key.D))
                    EyePos += right * (float)elapsedTime * MoveSpeed;
                if (KeyHeld(Key.W))
                    EyePos += forward * (float)elapsedTime * MoveSpeed;
                if (KeyHeld(Key.S))
                    EyePos -= forward * (float)elapsedTime * MoveSpeed;
                if (KeyHeld(Key.Q))
                    EyePos -= up * (float)elapsedTime * MoveSpeed;
                if (KeyHeld(Key.E))
                    EyePos += up * (float)elapsedTime * MoveSpeed;
                if (KeyHeld(Key.LeftShift))
                    MoveSpeed /= 2.0f;

                if (KeyHeld(Key.F))
                    gameobjects[0].position.X -= MoveSpeed * (float)elapsedTime;
                if (KeyHeld(Key.H))
                    gameobjects[0].position.X += MoveSpeed * (float)elapsedTime;
                if (KeyHeld(Key.T))
                    gameobjects[0].position.Z += MoveSpeed * (float)elapsedTime;
                if (KeyHeld(Key.G))
                    gameobjects[0].position.Z -= MoveSpeed * (float)elapsedTime;
                if (KeyHeld(Key.R))
                    gameobjects[0].position.Y -= MoveSpeed * (float)elapsedTime;
                if (KeyHeld(Key.Y))
                    gameobjects[0].position.Y += MoveSpeed * (float)elapsedTime;

                if (KeyHeld(Key.Right))
                    gameobjects[0].rotation.Y -= MoveSpeed * 10.0f * (float)elapsedTime;
                if (KeyHeld(Key.Left))
                    gameobjects[0].rotation.Y += MoveSpeed * 10.0f * (float)elapsedTime;
                if (KeyHeld(Key.Up))
                    gameobjects[0].rotation.X += MoveSpeed * 10.0f * (float)elapsedTime;
                if (KeyHeld(Key.Down))
                    gameobjects[0].rotation.X -= MoveSpeed * 10.0f * (float)elapsedTime;
            }
        }

        static void Clear()
        {
            for (int i = 0; i < Width * Height; i++)
            {
                display[i] = ' ';
                depthBuffer[i] = float.PositiveInfinity;
            }
        }

        static int TriangleClipAgainstPlane(Vector3 planeP, Vector3 planeN, ref Triangle tri, out Triangle tri1, out Triangle tri2)
        {
            planeN.Normalize();
            float dist(Vector4 p)
            {
                return (Vector3.Dot(planeN, (Vector3)p) - Vector3.Dot(planeN, planeP));
            }

            Vector4[] insidep = new Vector4[3]; int nInsidePointCount = 0;
            Vector4[] outsidep = new Vector4[3]; int nOutsidePointCount = 0;

            float d0 = dist(tri.v[0]);
            float d1 = dist(tri.v[1]);
            float d2 = dist(tri.v[2]);

            if (d0 >= 0) { insidep[nInsidePointCount++] = tri.v[0]; }
            else { outsidep[nOutsidePointCount++] = tri.v[0]; }
            if (d1 >= 0) { insidep[nInsidePointCount++] = tri.v[1]; }
            else { outsidep[nOutsidePointCount++] = tri.v[1]; }
            if (d2 >= 0) { insidep[nInsidePointCount++] = tri.v[2]; }
            else { outsidep[nOutsidePointCount++] = tri.v[2]; }

            tri1 = new Triangle();
            tri2 = new Triangle();

            if (nInsidePointCount == 0)
            {
                return 0;
            }
            else if (nInsidePointCount == 3)
            {
                tri1 = tri;
                return 1;
            }
            else if (nInsidePointCount == 1 && nOutsidePointCount == 2)
            {
                tri1.v[0] = insidep[0];
                tri1.v[1] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[0]);
                tri1.v[2] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[1]);
                return 1;
            }
            else if (nInsidePointCount == 2 && nOutsidePointCount == 1)
            {
                tri1.v[0] = insidep[0];
                tri1.v[1] = VectorIntersectPlane(planeP, planeN, insidep[1], outsidep[0]);
                tri1.v[2] = insidep[1];
                tri2.v[0] = insidep[0];
                tri2.v[1] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[0]);
                tri2.v[2] = VectorIntersectPlane(planeP, planeN, insidep[1], outsidep[0]);
                return 2;
            }
            else { return 0; }
        }

        static Vector4 VectorIntersectPlane(Vector3 planeP, Vector3 planeN, Vector4 lineStart, Vector4 lineEnd)
        {
            float planeD = Vector3.Dot(planeN, planeP);
            float ad = Vector3.Dot((Vector3)lineStart, planeN);
            float bd = Vector3.Dot((Vector3)lineEnd, planeN);
            float t = (planeD - ad) / (bd - ad);
            return lineStart + t * (lineEnd - lineStart);
        }

        static char GetColour(float lum)
        {
            if (lum <= 0.0f)
                return EMPTY_BLOCK;
            if (lum < 0.25f)
                return QUARTER_BLOCK;
            if (lum < 0.5f)
                return HALF_BLOCK;
            if (lum < 0.75f)
                return THREE_QUARTER_BLOCK;
            return FULL_BLOCK;
        }

        static char GetColor(float lum)
        {
            if (lum < 0.2f)
                return EMPTY_BLOCK;
            if (lum < 0.4f)
                return QUARTER_BLOCK;
            if (lum < 0.6f)
                return HALF_BLOCK;
            if (lum < 0.8f)
                return THREE_QUARTER_BLOCK;
            return FULL_BLOCK;
        }

        static void Draw(int x, int y, char col)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                display[y * Width + x] = col;
            }
        }

        static void DrawDepth(int x, int y, float z, char col)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                int i = y * Width + x;
                if (!debug)
                {
                    if (depthBuffer[i] > z)
                    {
                        display[i] = GetColor(z - 0.1f);
                        depthBuffer[i] = z;
                        //display[i] = col;
                        //depthBuffer[i] = z;
                    }
                }
                else
                {
                    display[i] = FULL_BLOCK;
                }
            }
        }

        static void DrawLine(int x1, int y1, int x2, int y2, char col)
        {
            int x, y, xe, ye;
            int dx = x2 - x1;
            int dy = y2 - y1;
            int dx1 = Math.Abs(dx);
            int dy1 = Math.Abs(dy);
            int px = 2 * dy1 - dx1;
            int py = 2 * dx1 - dy1;
            if (dy1 <= dx1)
            {
                if (dx >= 0)
                {
                    x = x1;
                    y = y1;
                    xe = x2;
                }
                else
                {
                    x = x2;
                    y = y2;
                    xe = x1;
                }
                Draw(x, y, col);
                while(x < xe)
                {
                    x += 1;
                    if (px < 0)
                        px += 2 * dy1;
                    else
                    {
                        if ((dx < 0 && dy < 0) || (dx > 0 && dy > 0))
                            y += 1;
                        else
                            y -= 1;
                        px += 2 * (dy1 - dx1);
                    }
                    Draw(x, y, col);
                }
            }
            else
            {
                if (dy >= 0)
                {
                    x = x1;
                    y = y1;
                    ye = y2;
                }
                else
                {
                    x = x2;
                    y = y2;
                    ye = y1;
                }
                Draw(x, y, col);
                for (int i = 0; y < ye; i++)
                {
                    y += 1;
                    if (py <= 0)
                        py += 2 * dx1;
                    else
                    {
                        if ((dx < 0 && dy < 0) || (dx > 0 && dy > 0))
                            x += 1;
                        else
                            x -= 1;
                        py += 2 * (dx1 - dy1);
                    }
                    Draw(x, y, col);
                }
            }
        }

        static void DrawTriangle(int x1, int y1, int x2, int y2, int x3, int y3, char col)
        {
            DrawLine(x1, y1, x2, y2, col);
            DrawLine(x2, y2, x3, y3, col);
            DrawLine(x1, y1, x3, y3, col);
        }

        static void FillTriangle(int x1, int y1, float z1, int x2, int y2, float z2, int x3, int y3, float z3, char col)
        {
            int xx1, xx2, xx3, yy1, yy2, yy3;
            float zz1, zz2, zz3;
            if (y1 <= y2)
            {
                if (y2 <= y3)
                { // y1 < y2 < y3
                    yy1 = y1;
                    xx1 = x1;
                    zz1 = z1;
                    yy2 = y2;
                    xx2 = x2;
                    zz2 = z2;
                    yy3 = y3;
                    xx3 = x3;
                    zz3 = z3;
                }
                else
                {
                    if (y1 <= y3)
                    { // y1 < y3 < y2
                        yy1 = y1;
                        xx1 = x1;
                        zz1 = z1;
                        yy2 = y3;
                        xx2 = x3;
                        zz2 = z3;
                        yy3 = y2;
                        xx3 = x2;
                        zz3 = z2;
                    }
                    else
                    { // y3 < y1 < y2
                        yy1 = y3;
                        xx1 = x3;
                        zz1 = z3;
                        yy2 = y1;
                        xx2 = x1;
                        zz2 = z1;
                        yy3 = y2;
                        xx3 = x2;
                        zz3 = z2;
                    }
                }
            }
            else
            {
                if (y1 <= y3)
                { // y2 < y1 < y3
                    yy1 = y2;
                    xx1 = x2;
                    zz1 = z2;
                    yy2 = y1;
                    xx2 = x1;
                    zz2 = z1;
                    yy3 = y3;
                    xx3 = x3;
                    zz3 = z3;
                }
                else
                {
                    if (y2 <= y3)
                    { // y2 < y3 < y1
                        yy1 = y2;
                        xx1 = x2;
                        zz1 = z2;
                        yy2 = y3;
                        xx2 = x3;
                        zz2 = z3;
                        yy3 = y1;
                        xx3 = x1;
                        zz3 = z1;
                    }
                    else
                    { // y3 < y2 < y1
                        yy1 = y3;
                        xx1 = x3;
                        zz1 = z3;
                        yy2 = y2;
                        xx2 = x2;
                        zz2 = z2;
                        yy3 = y1;
                        xx3 = x1;
                        zz3 = z1;
                    }
                }
            }
            int dx1 = xx2 - xx1;
            int dy1 = yy2 - yy1;
            float dz1 = zz2 - zz1;
            int dx2 = xx3 - xx1;
            int dy2 = yy3 - yy1;
            float dz2 = zz3 - zz1;
            float daxs = 0.0f, dbxs = 0.0f, dazs = 0.0f, dbzs;
            if (dy1 != 0.0f)
            {
                daxs = (float)dx1 / Math.Abs(dy1);
                dazs = dz1 / Math.Abs(dy1);
            }
            if (dy2 != 0.0f)
            {
                dbxs = (float)dx2 / Math.Abs(dy2);
                dbzs = dz2 / Math.Abs(dy2);
            }
            else
            {
                dbzs = dz2 / Math.Abs(dx2);
            }
            if (dy1 != 0.0f) // top half
            {
                for (int i = yy1; i <= yy2; i++)
                {
                    int ax = (int)(xx1 + (i - yy1) * daxs);
                    int bx = (int)(xx1 + (i - yy1) * dbxs);
                    float az = zz1 + (i - yy1) * dazs;
                    float bz = zz1 + (i - yy1) * dbzs;
                    if (ax > bx)
                    {
                        int temp = bx;
                        bx = ax;
                        ax = temp;
                        float temp2 = bz;
                        bz = az;
                        az = temp2;
                    }
                    for (int j = ax; j < bx; j++)
                    {
                        float d = az + (bz - az) * (j - ax) / (bx - ax);
                        DrawDepth(j, i, d, col);
                    }
                }
            }
            dx1 = xx3 - xx2;
            dy1 = yy3 - yy2;
            if (dy1 != 0)
            {
                daxs = (float)dx1 / Math.Abs(dy1);
                dazs = dz1 / Math.Abs(dy1);
            }
            if (dy1 != 0.0f) // bottom half
            {
                for (int i = yy2; i <= yy3; i++)
                {
                    int ax = (int)(xx2 + (i - yy2) * daxs);
                    int bx = (int)(xx1 + (i - yy1) * dbxs);
                    float az = zz2 + (i - yy2) * dazs;
                    float bz = zz1 + (i - yy1) * dbzs;
                    if (ax > bx)
                    {
                        int temp = bx;
                        bx = ax;
                        ax = temp;
                        float temp2 = bz;
                        bz = az;
                        az = temp2;
                    }
                    for (int j = ax; j < bx; j++)
                    {
                        float d = az + (bz - az) * (j - ax) / (bx - ax);
                        DrawDepth(j, i, d, col);
                    }
                }
            }
        }

        static void Render()
        {
            Console.SetCursorPosition(0, 0);
            Console.Write(display);
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
        }

        static void InitializeMouse()
        {
            mouse = new Mouse(new DirectInput());
            mouse.Acquire();
            var state = mouse.GetCurrentState();
            var allButtons = state.Buttons;
            buttons = new Button[allButtons.Length];
            for (int i = 0; i < allButtons.Length; i++)
                buttons[i] = new Button();
            DeltaMousePos = new Vector2(state.X, state.Y);
            DeltaMouseScroll = state.Z;
        }

        static void InitializeKeyboard()
        {
            keyboard = new Keyboard(new DirectInput());
            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
            var state = keyboard.GetCurrentState();
            var allKeys = state.AllKeys;
            cheyArray = new Chey[allKeys.Count];
            for (int i = 0; i < allKeys.Count; i++)
                cheyArray[i] = new Chey(allKeys[i]);
        }

        static void GetMouseData()
        {
            mouse.Poll();
            var state = mouse.GetCurrentState();
            var butts = state.Buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool pressed = butts[i];
                buttons[i].Down = buttons[i].Raised && pressed;
                buttons[i].Up = buttons[i].Held && !pressed;
                buttons[i].Held = pressed;
                buttons[i].Raised = !pressed;
            }
            DeltaMousePos = new Vector2(state.X, state.Y);
            DeltaMouseScroll = state.Z;
        }

        static void GetKeys()
        {
            keyboard.Poll();
            var state = keyboard.GetCurrentState();
            for (int i = 0; i < cheyArray.Length; i++)
            {
                bool pressed = state.IsPressed(cheyArray[i].key);
                cheyArray[i].Down = cheyArray[i].Raised && pressed;
                cheyArray[i].Up = cheyArray[i].Held && !pressed;
                cheyArray[i].Held = pressed;
                cheyArray[i].Raised = !pressed;
            }
        }

        static Chey FindChey(Key key)
        {
            for (int i = 0; i < cheyArray.Length; i++)
            {
                if (cheyArray[i].key == key)
                    return cheyArray[i];
            }
            return null;
        }
        
        static bool KeyDown(Key key)
        {
            return FindChey(key).Down;
        }
        
        static bool KeyUp(Key key)
        {
            return FindChey(key).Up;
        }
        
        static bool KeyHeld(Key key)
        {
            return FindChey(key).Held;
        }
        
        static bool KeyRaised(Key key)
        {
            return FindChey(key).Raised;
        }

        static bool ButtonDown(int button)
        {
            return buttons[button].Down;
        }

        static bool ButtonUp(int button)
        {
            return buttons[button].Up;
        }

        static bool ButtonHeld(int button)
        {
            return buttons[button].Held;
        }

        static bool ButtonRaised(int button)
        {
            return buttons[button].Raised;
        }

        [DllImport("user32.dll")]
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();
    }
}
