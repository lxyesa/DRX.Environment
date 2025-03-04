using DLTools.Module.Contorls;
using DLTools.Scripts;
using Drx.Sdk.Events;
using Drx.Sdk.Handle;
using Drx.Sdk.Input;
using Drx.Sdk.Memory;
using Drx.Sdk.Script.Functions;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static KopiLua.Lua;
using Console = System.Console;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace DLTools.Module
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : System.Windows.Controls.Page
    {
        private WpfHotkeyManager? _hotkeyManager;
        private MemoryHook? _hook;

        // 基地址
        private IntPtr[] CloneItemBaseAddress;
        private IntPtr[] safeAreaUseWeaponAddress;
        private IntPtr[] crash12Address;
        private IntPtr[] immuneExplosionDmgAddress;
        private IntPtr[] crash5Address;
        private IntPtr[] unlimitedDurabilityAddress;
        private IntPtr[] autoDodgeTackleAddress;
        private IntPtr[] unconditionallyTackleAddress;

        // HOOK 类
        private HookInst safeAreaUseWeaponHookInst;
        private HookInst unlimitedDurabilityHookInst;
        private HookInst autoDodgeTackleHookInst;

        // 监听器
        private ProcessListener pListener;

        public HomePage()
        {
            InitializeComponent();
            Inst();
        }

        private void Inst()
        {
            try
            {
                _hook = new MemoryHook("DyingLightGame");
                Tip0Suss.IsOpen = true;
                Main.IsEnabled = true;
            }
            catch
            {
                Tip0Err.IsOpen = true;
                System.Console.WriteLine("获取进程 DyingLightGame 失败，请检查是否启动游戏");
            }

            try
            {
                var _debugHook = new MemoryHook("DLTools");
                var offset = MemoryWriter.Alloc(new nint(0x7FF6A7D52000), 1024, "DLTools");
                var _debugHookInst = _debugHook.CreateHook("debugHook", offset);

                System.Console.WriteLine(Values.IntptrToHex(offset));

                _debugHookInst.AddAddressVariable("test", offset);
                _debugHookInst.AddAsm("mov [&alloc(test)], rax");
                _debugHookInst.AddAsm("nop");

                _debugHookInst.Enable();

            }
            catch(Exception e)
            {
                System.Console.WriteLine($"执行失败:{e.Message}");
            }



            // 注册热键
            RegisterHotkeys();

            if (GlobalSettings.Instance.AppListenerProcess)
            {
                pListener = new("DyingLightGame", 500);

                pListener.ProcessStarted += (sender, e) => {
                    this.Dispatcher.Invoke(() =>
                    {
                        if (_hook == null)
                        {
                            _hook = new MemoryHook("DyingLightGame");
                        }
                        Tip0Err.IsOpen = false;
                        Tip0Suss.IsOpen = true;
                        Main.IsEnabled = true;
                    });
                };
                pListener.ProcessStopped += (sender, e) => {
                    this.Dispatcher.Invoke(() =>
                    {
                        _hook = null;
                        CloneItemBaseAddress = null;
                        safeAreaUseWeaponAddress = null;
                        crash12Address = null;
                        immuneExplosionDmgAddress = null;
                        crash5Address = null;
                        unlimitedDurabilityAddress = null;

                        safeAreaUseWeaponHookInst = null;
                        unlimitedDurabilityHookInst = null;

                        Tip0Err.IsOpen = true;
                        Tip0Suss.IsOpen = false;
                        Main.IsEnabled = true;
                    });
                };

                pListener.Start();
            }
        }


        private void RegisterHotkeys()
        {
            if (_hotkeyManager == null) return;
            
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts)
            {
                if (ts.Tag == null) return;

                switch (ts.Tag)
                {
                    case "CloneItem":
                        _ = CloneItem(ts);
                        break;
                    default:
                        break;
                }
            }
        }

        // 克隆物品
        private async Task<bool> CloneItem(ToggleSwitch ts)
        {
            if (ts.IsOn)
            {
                byte[] target = [0x48, 0x8B, 0xF0, 0xEB];
                try
                {
                    // 特征码搜索，防止多次搜索，提高性能
                    if (CloneItemBaseAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            // 通过模式搜索 CloneItem 函数的基址
                            var pattern = new byte[] {
                                0x48, 0x8B, 0xf0, 0x74, 0x00, 0x48, 0x8b, 0x44, 0x24, 0x48
                            };
                            var patternStr = "48 8B F0 74 ** 48 8B 44 24 48";
                            CloneItemBaseAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", patternStr, "gamedll_x64_rwdi.dll");
                            System.Console.WriteLine(
                                "CloneItem BaseAddr: 0x" + Values.IntptrToHex(CloneItemBaseAddress[0]));

                        });
                    }

                    MemoryWriter.WriteMemory("DyingLightGame", CloneItemBaseAddress[0], target);
                    return true;
                }
                catch
                {
                    System.Console.WriteLine("Hook CloneItem 失败");
                    ts.SetValue(ToggleSwitch.IsOnProperty, false);
                    return false;
                }
            }
            else
            {
                byte[] target = [0x48, 0x8B, 0xF0, 0x74];
                try
                {
                    // 同上
                    if (CloneItemBaseAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            // 通过模式搜索 CloneItem 函数的基址
                            var pattern = new byte[] {
                                                    0x48, 0x8B, 0xf0, 0x74, 0x00, 0x48, 0x8b, 0x44, 0x24, 0x48
                                                };
                            var patternStr = "48 8B F0 74 ** 48 8B 44 24 48";
                            CloneItemBaseAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", patternStr, "gamedll_x64_rwdi.dll");
                            System.Console.WriteLine(
                                "CloneItem BaseAddr: 0x" + Values.IntptrToHex(CloneItemBaseAddress[0]));

                        });
                    }

                    MemoryWriter.WriteMemory("DyingLightGame", CloneItemBaseAddress[0], target);
                    return true;
                }
                catch
                {
                    System.Console.WriteLine("Hook CloneItem 失败");
                    ts.SetValue(ToggleSwitch.IsOnProperty, false);
                    return false;
                }
            }
        }

        private void CardStatusChanged(object sender, RoutedEventArgs e)
        {
            var card = sender as ModifCard;

            if (card == null) return;
            if (card.Tag == null) return;

            switch (card.Tag)
            {
                case "modif2":
                    SafeUseTool(card.Status);
                    break;
                case "crash12":
                    Crash12(card.Status);
                    break;
                case "immuneExplosionDmg":
                    ImmuneExplosionDmg(card.Status);
                    break;
                case "crash5":
                    Crash5(card.Status);
                    break;
                case "unlimitedDurability":
                    UnlimitedDurability(card.Status);
                    break;
                case "autoDodgeTackle":
                    AutoDodgeTackle(card.Status);
                    break;
                case "unconditionallyTackle":
                    UnconditionallyTackle(card.Status);
                    break;
                default:
                    break;
            }
        }

        private async void UnconditionallyTackle(bool status)
        {
            if (status)
            {
                try
                {
                    var pattern = "C6 43 28 00 48 8B 74 24 60";

                    if (unconditionallyTackleAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            unconditionallyTackleAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    // 扫描地址后如果地址还是空的话，说明没有找到指定的地址
                    if (unconditionallyTackleAddress == null) throw new Exception("未找到指定地址:"+ pattern);

                    var offset = unconditionallyTackleAddress[0] + 0x03;

                    MemoryWriter.WriteMemory("DyingLightGame", offset, "01");
                }
                catch (Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    var offset = unconditionallyTackleAddress[0] + 0x03;
                    MemoryWriter.WriteMemory("DyingLightGame", offset, "00");
                }
                catch (Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
        }

        private async void AutoDodgeTackle(bool status)
        {
            if (status)
            {
                try
                {
                    var pattern = "73 1F 48 83 BB 48 02 00 00 00";

                    if (autoDodgeTackleAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            autoDodgeTackleAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (autoDodgeTackleHookInst == null)
                    {
                        var offset = autoDodgeTackleAddress[0];
                        autoDodgeTackleHookInst = _hook.CreateHook("autoDodgeTackle", offset);

                        autoDodgeTackleHookInst.AddAsm("nop");
                        autoDodgeTackleHookInst.AddAsm("cmp qword ptr [rbx+0x00000248],00");
                        autoDodgeTackleHookInst.AddAsm("",true);
                        autoDodgeTackleHookInst.AddAsm("jae {gamedll_x64_rwdi.dll+D657EE}");
                        autoDodgeTackleHookInst.AddAsm("cmp qword ptr [rbx+0x00000248],00");
                        autoDodgeTackleHookInst.AddAsm("", true);

                        autoDodgeTackleHookInst.AddJumpAsm("nop");
                        autoDodgeTackleHookInst.AddJumpAsm("nop");
                        autoDodgeTackleHookInst.AddJumpAsm("nop");
                        autoDodgeTackleHookInst.AddJumpAsm("nop");
                        autoDodgeTackleHookInst.AddJumpAsm("nop");
                    }

                    autoDodgeTackleHookInst.Enable();
                }
                catch (Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (autoDodgeTackleHookInst == null) return;
                    autoDodgeTackleHookInst.Disable();
                }
                catch (Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
        }

        private async void SafeUseTool(bool status)
        {
            if (status)
            {
                try
                {
                    var pattern = "25 80 7B 50 00 74 0C 83 BB A8 09 00 00 00";
                    if (safeAreaUseWeaponAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            safeAreaUseWeaponAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (safeAreaUseWeaponHookInst == null)
                    {
                        var offset = safeAreaUseWeaponAddress[0] + 0x07;
                        safeAreaUseWeaponHookInst = _hook.CreateHook("modif2", offset);

                        safeAreaUseWeaponHookInst.AddAsm("mov dword ptr [rbx+0x000009A8],00");
                        safeAreaUseWeaponHookInst.AddAsm("cmp dword ptr [rbx+0x000009A8],00");
                    }

                    safeAreaUseWeaponHookInst.Enable();
                }
                catch(Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (safeAreaUseWeaponHookInst == null) return;
                    safeAreaUseWeaponHookInst.Disable();
                }
                catch(Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
        }

        private async void UnlimitedDurability(bool status)
        {
            if (status)
            {
                try
                {
                    var pattern = "F3 0F 5D C6 0F 28 F0 0F";
                    if (unlimitedDurabilityAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            unlimitedDurabilityAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (unlimitedDurabilityHookInst == null)
                    {
                        var offset = unlimitedDurabilityAddress[0];
                        unlimitedDurabilityHookInst = _hook.CreateHook("unlimitedDurability", offset);

                        unlimitedDurabilityHookInst.AddAsm("movaps xmm6,xmm0");
                        unlimitedDurabilityHookInst.AddJumpAsm("nop");
                        unlimitedDurabilityHookInst.AddJumpAsm("nop");
                    }

                    unlimitedDurabilityHookInst.Enable();
                }
                catch(Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (unlimitedDurabilityHookInst == null) return;
                    unlimitedDurabilityHookInst.Disable();
                }
                catch(Exception e)
                {
                    System.Console.WriteLine($"执行失败:{e.Message}");
                }
            }
        }

        private async void Crash12(bool status)
        {
            // 12代崩溃.
            if (status)
            {
                try
                {
                    var pattern = "FF 12 EB 05 B8 FF FF FF FF 42";
                    if (crash12Address == null)
                    {
                        await Task.Run(() =>
                        {
                            crash12Address = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "engine_x64_rwdi.dll");
                        });
                    }

                    var baseAddress = crash12Address[0];

                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "90 90 EB 05 B8 FF FF FF FF");
                }
                catch
                {
                    System.Console.WriteLine("功能开启失败。");
                }
            }
            else
            {
                try
                {
                    var baseAddress = crash12Address[0];
                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "FF 12 EB 05 B8 FF FF FF FF");
                }
                catch
                {
                    System.Console.WriteLine("功能关闭失败。");
                }
            }
        }

        private async void ImmuneExplosionDmg(bool status)
        {
            // 免疫爆炸伤害
            if (status)
            {
                try
                {
                    var pattern = "FF 53 20 48 8B 5C 24 78";
                    if (immuneExplosionDmgAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            immuneExplosionDmgAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    var baseAddress = immuneExplosionDmgAddress[0];

                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "90 90 90 48 8B 5C 24 78");
                }
                catch
                {
                    System.Console.WriteLine("功能开启失败。");
                }
            }
            else
            {
                try
                {
                    var baseAddress = immuneExplosionDmgAddress[0];
                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "FF 53 20 48 8B 5C 24 78");
                }
                catch
                {
                    System.Console.WriteLine("功能关闭失败。");
                }
            }
        }
        
        private async void Crash5(bool status)
        {
            // 5带崩溃
            if (status)
            {
                try
                {
                    var pattern = "48 8D 41 04 48 89 93 98 00 00 00 48 3D 4C 0E 00 00 77 23 8B 87 84 0E";
                    if (crash5Address == null)
                    {
                        await Task.Run(() =>
                        {
                            crash5Address = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    var baseAddress = crash5Address[0];

                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "90 90 90 90 48 89 93 98 00 00 00");
                }
                catch
                {
                    System.Console.WriteLine("功能开启失败。");
                }
            }
            else
            {
                try
                {
                    var baseAddress = crash5Address[0];
                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "48 8D 41 04 48 89 93 98 00 00 00");
                }
                catch
                {
                    System.Console.WriteLine("功能关闭失败。");
                }
            }
        }

        private void CardGroupItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("功能开发中，敬请期待。");
        }
    }
}
