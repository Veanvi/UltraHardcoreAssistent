using AutoIt;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UltraHardcoreAssistent.Bot.Vision;

namespace UltraHardcoreAssistent.Bot
{
    public class GameBot
    {
        private CancellationTokenSource cancelTokenSource;

        private Task worker;

        public GameBot()
        {
            Eye = new Eye();
        }

        public event Action<string> OnArrowsPressed;

        public event Action<string> OnTextEntered;

        public bool IsWork { get; private set; }

        private Eye Eye { get; set; }

        public async void StartWorkAsync()
        {
            if (worker?.Status == TaskStatus.Running)
                return;
            cancelTokenSource = new CancellationTokenSource();
            var token = cancelTokenSource.Token;
            uint currentKeyboardLayout = 1033;

            worker = Task.Run(() =>
            {
                IsWork = true;
                while (cancelTokenSource.IsCancellationRequested == false)
                {
                    try
                    {
                        bool isGameActive = AutoItX.WinGetTitle("[active]") == "ultra_hardcore";
#if DEBUG
                        isGameActive = true;
#endif
                        if (isGameActive)
                        {
                            currentKeyboardLayout = GetKeyboardLayout();
                            ActivateKeyboardLayout((uint)1033, KeyboardLayoutFlags.KLF_SETFORPROCESS);

                            AutoItX.AutoItSetOption("SendKeyDownDelay", 20);
                            //AutoItX.Send("{ENTER}");
                            if (token.IsCancellationRequested)
                                return;
                            var timerPos = Eye.GetTimerPosition();
                            switch (timerPos)
                            {
                                case Eye.TimerPosition.Top:
                                    AutoItX.Send("{UP}");
                                    OnArrowsPressed("{UP}");
                                    break;

                                case Eye.TimerPosition.Bottom:
                                    AutoItX.Send("{DOWN}");
                                    OnArrowsPressed("{DOWN}");
                                    break;

                                case Eye.TimerPosition.Left:
                                    AutoItX.Send("{LEFT}");
                                    OnArrowsPressed("{LEFT}");
                                    break;

                                case Eye.TimerPosition.Right:
                                    AutoItX.Send("{RIGHT}");
                                    OnArrowsPressed("{RIGHT}");
                                    break;

                                case Eye.TimerPosition.Empty:
                                    break;
                            }

                            AutoItX.AutoItSetOption("SendKeyDownDelay", 5);
                            if (token.IsCancellationRequested)
                                return;
                            string text = Eye.GetText();
                            AutoItX.Send(text);
                            //foreach (var ch in text)
                            //{
                            //    AutoItX.Send(ch.ToString());
                            //    if (token.IsCancellationRequested)
                            //        return;
                            //    isGameActive = AutoItX.WinGetTitle("[active]") == "ultra_hardcore";
                            //    if (isGameActive == false)
                            //        break;
                            //}

                            OnTextEntered(text);
                        }
                        else
                        {
                            token.WaitHandle.WaitOne(300);
                        }
                    }
                    catch (Exception e)
                    {
                    }
                    token.WaitHandle.WaitOne(2);
                }
                IsWork = false;
            }, token);
            await worker;
            ActivateKeyboardLayout(currentKeyboardLayout, KeyboardLayoutFlags.KLF_SETFORPROCESS);
        }

        public async void StopWorkAsync()
        {
            if (cancelTokenSource?.IsCancellationRequested == false)
            {
                cancelTokenSource.Cancel();
                await Task.Run(() => worker.Wait());
            }
        }

        #region SwitchKeyboardLayouts

        private enum KeyboardLayoutFlags : uint
        {
            KLF_ACTIVATE = 0x00000001,
            KLF_SETFORPROCESS = 0x00000100
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint ActivateKeyboardLayout(uint hkl, KeyboardLayoutFlags Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern ushort GetKeyboardLayout([In] int idThread);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(
            [In] IntPtr hWnd,
            [Out, Optional] IntPtr lpdwProcessId);

        private ushort GetKeyboardLayout()
        {
            return GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        }

        #endregion SwitchKeyboardLayouts
    }
}