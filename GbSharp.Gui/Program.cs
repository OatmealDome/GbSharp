using GbSharp.Controller;
using System;
#if DEBUG
using System.Diagnostics;
#endif
using System.IO;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using static SDL2.SDL;

namespace GbSharp.Gui
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("dotnet GbSharp.Gui.dll <rom> [bootrom]");
                return;
            }

            // Initialize audio
            SDL_InitSubSystem(SDL_INIT_AUDIO);

            SDL_AudioSpec desiredSpec = new SDL_AudioSpec();
            desiredSpec.freq = 44100; // CD quality
            desiredSpec.format = AUDIO_F32;
            desiredSpec.channels = 2;
            desiredSpec.samples = 1024;

            SDL_AudioSpec obtainedSpec;
            uint id = SDL_OpenAudioDevice(null, 0, ref desiredSpec, out obtainedSpec, 0);

            SDL_PauseAudioDevice(id, 0);

            // Create the GameBoy instance and load a ROM
            GameBoy gameBoy = new GameBoy();
            gameBoy.LoadRom(File.ReadAllBytes(args[0]), args.Length > 1 ? File.ReadAllBytes(args[1]) : null);

            // When the APU has enough samples, this event handler will be executed
            gameBoy.SamplesReady += (samples) =>
            {
                unsafe
                {
                    fixed (float* ptr = samples)
                    {
                        SDL_QueueAudio(id, (IntPtr)ptr, (uint)(samples.Length * 4));
                    }
                }

#if !DEBUG
                // Wait until queue is exhausted
                // On debug builds, don't do this as video is prioritized over audio
                while (SDL_GetQueuedAudioSize(id) > 4096 * 4)
                {
                    ;
                }
#endif
            };

            // Create a window to render to using Veldrid
            WindowCreateInfo windowCreateInfo = new WindowCreateInfo()
            {
                X = 100,
                Y = 100,
                WindowWidth = 160 * 2,
                WindowHeight = 144 * 2,
                WindowTitle = "GbSharp.Gui"
            };

            // Create a window to render to using Veldrid
            Sdl2Window window;
            GraphicsDevice graphicsDevice;

            VeldridStartup.CreateWindowAndGraphicsDevice(windowCreateInfo, out window, out graphicsDevice);

            Renderer renderer = new Renderer(graphicsDevice);

#if DEBUG
            Stopwatch stopwatch = new Stopwatch();
#endif

            // Run emulation
            while (window.Exists)
            {
#if DEBUG
                double newElapsed = stopwatch.Elapsed.TotalMilliseconds;
#endif

                gameBoy.RunFrame();

#if DEBUG
                // Choppy audio, but we shouldn't need this for debugging anyway
                while (stopwatch.Elapsed.TotalMilliseconds - newElapsed < 16.7)
                {
                    ;
                }
#endif

                InputSnapshot snapshot = window.PumpEvents();
                foreach (KeyEvent keyEvent in snapshot.KeyEvents)
                {
                    switch (keyEvent.Key)
                    {
                        case Key.W:
                            gameBoy.UpdateControllerKeyState(ControllerKey.DPadUp, keyEvent.Down);
                            break;
                        case Key.S:
                            gameBoy.UpdateControllerKeyState(ControllerKey.DPadDown, keyEvent.Down);
                            break;
                        case Key.A:
                            gameBoy.UpdateControllerKeyState(ControllerKey.DPadLeft, keyEvent.Down);
                            break;
                        case Key.D:
                            gameBoy.UpdateControllerKeyState(ControllerKey.DPadRight, keyEvent.Down);
                            break;
                        case Key.Minus:
                            gameBoy.UpdateControllerKeyState(ControllerKey.Select, keyEvent.Down);
                            break;
                        case Key.Plus:
                            gameBoy.UpdateControllerKeyState(ControllerKey.Start, keyEvent.Down);
                            break;
                        case Key.Comma:
                            gameBoy.UpdateControllerKeyState(ControllerKey.B, keyEvent.Down);
                            break;
                        case Key.Period:
                            gameBoy.UpdateControllerKeyState(ControllerKey.A, keyEvent.Down);
                            break;
                    }
                }

                renderer.Draw(gameBoy.GetPixelOutput());
            }

#if DEBUG
            stopwatch.Stop();
#endif

            renderer.DisposeResources();
        }

    }
}
