using System.Windows;
using DLTools.Module.Contorls;
using DLTools.Scripts;
using Drx.Sdk.Memory;
using iNKORE.UI.WPF.Modern.Controls;
using Console = System.Console;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = System.Windows.Controls.Page;

#pragma warning disable CS0649 // 从未对字段赋值，字段将一直保持其默认值

namespace DLTools.Module
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        private MemoryHook? _hook;

        // 基地址
        private IntPtr[]? _cloneItemBaseAddress;
        private IntPtr[]? _safeAreaUseWeaponAddress;
        private IntPtr[]? _crash12Address;
        private IntPtr[]? _immuneExplosionDmgAddress;
        private IntPtr[]? _crash5Address;
        private IntPtr[]? _unlimitedDurabilityAddress;
        private IntPtr[]? _autoDodgeTackleAddress;
        private IntPtr[]? _unconditionallyTackleAddress;

        // HOOK 类
        private HookInst? _safeAreaUseWeaponHookInst;
        private HookInst? _unlimitedDurabilityHookInst;
        private HookInst? _autoDodgeTackleHookInst;

        public HomePage()
        {
            Inst();
        }

        private void Inst()
        {
            try
            {
                _hook = new MemoryHook("DyingLightGame");
            }
            catch
            {
                Console.WriteLine("获取进程 DyingLightGame 失败，请检查是否启动游戏");
            }
            
            // 注册热键
            RegisterHotkeys();

            if (!GlobalSettings.Instance.AppListenerProcess) return;
            {
                Console.WriteLine($"[+] [Event] 监听器器已就绪。");
            }
        }


        private void RegisterHotkeys()
        {
            // if (_hotkeyManager == null) return;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch ts) return;
            if (ts.Tag == null) return;

            switch (ts.Tag)
            {
                case "CloneItem":
                    _ = CloneItem(ts);
                    break;
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
                    if (_cloneItemBaseAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            // 通过模式搜索 CloneItem 函数的基址
                            var patternStr = "48 8B F0 74 ** 48 8B 44 24 48";
                            _cloneItemBaseAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", patternStr, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (_cloneItemBaseAddress != null)
                        MemoryWriter.WriteMemory("DyingLightGame", _cloneItemBaseAddress[0], target);
                    return true;
                }
                catch
                {
                    Console.WriteLine("Hook CloneItem 失败");
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
                    if (_cloneItemBaseAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            // 通过模式搜索 CloneItem 函数的基址
                            var pattern = new byte[]
                            {
                                0x48, 0x8B, 0xf0, 0x74, 0x00, 0x48, 0x8b, 0x44, 0x24, 0x48
                            };
                            var patternStr = "48 8B F0 74 ** 48 8B 44 24 48";
                            _cloneItemBaseAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", patternStr, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (_cloneItemBaseAddress != null)
                        MemoryWriter.WriteMemory("DyingLightGame", _cloneItemBaseAddress[0], target);
                    return true;
                }
                catch
                {
                    Console.WriteLine("Hook CloneItem 失败");
                    ts.SetValue(ToggleSwitch.IsOnProperty, false);
                    return false;
                }
            }
        }

        private void CardStatusChanged(object sender, RoutedEventArgs e)
        {
            var card = sender as ModifCard;

            if (card?.Tag == null) return;

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
            }
        }

        private async void UnconditionallyTackle(bool status)
        {
            if (status)
            {
                try
                {
                    const string pattern = "C6 43 28 00 48 8B 74 24 60";

                    if (_unconditionallyTackleAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            _unconditionallyTackleAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    // 扫描地址后如果地址还是空的话，说明没有找到指定的地址
                    if (_unconditionallyTackleAddress == null) throw new Exception("未找到指定地址:" + pattern);

                    var offset = _unconditionallyTackleAddress[0] + 0x03;

                    MemoryWriter.WriteMemory("DyingLightGame", offset, "01");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (_unconditionallyTackleAddress == null) return;
                    var offset = _unconditionallyTackleAddress[0] + 0x03;
                    MemoryWriter.WriteMemory("DyingLightGame", offset, "00");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
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

                    if (_autoDodgeTackleAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            _autoDodgeTackleAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (_autoDodgeTackleHookInst == null)
                    {
                        var offset = _autoDodgeTackleAddress[0];
                        _autoDodgeTackleHookInst = _hook.CreateHook("autoDodgeTackle", offset);

                        _autoDodgeTackleHookInst.AddAsm("nop");
                        _autoDodgeTackleHookInst.AddAsm("cmp qword ptr [rbx+0x00000248],00");
                        _autoDodgeTackleHookInst.AddAsm("", true);
                        _autoDodgeTackleHookInst.AddAsm("jae {gamedll_x64_rwdi.dll+D657EE}");
                        _autoDodgeTackleHookInst.AddAsm("cmp qword ptr [rbx+0x00000248],00");
                        _autoDodgeTackleHookInst.AddAsm("", true);

                        _autoDodgeTackleHookInst.AddJumpAsm("nop");
                        _autoDodgeTackleHookInst.AddJumpAsm("nop");
                        _autoDodgeTackleHookInst.AddJumpAsm("nop");
                        _autoDodgeTackleHookInst.AddJumpAsm("nop");
                        _autoDodgeTackleHookInst.AddJumpAsm("nop");
                    }

                    _autoDodgeTackleHookInst.Enable();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (_autoDodgeTackleHookInst == null) return;
                    _autoDodgeTackleHookInst.Disable();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
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
                    if (_safeAreaUseWeaponAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            _safeAreaUseWeaponAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (_safeAreaUseWeaponHookInst == null)
                    {
                        var offset = _safeAreaUseWeaponAddress[0] + 0x07;
                        _safeAreaUseWeaponHookInst = _hook.CreateHook("modif2", offset);

                        _safeAreaUseWeaponHookInst.AddAsm("mov dword ptr [rbx+0x000009A8],00");
                        _safeAreaUseWeaponHookInst.AddAsm("cmp dword ptr [rbx+0x000009A8],00");
                    }

                    _safeAreaUseWeaponHookInst.Enable();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (_safeAreaUseWeaponHookInst == null) return;
                    _safeAreaUseWeaponHookInst.Disable();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
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
                    if (_unlimitedDurabilityAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            _unlimitedDurabilityAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    if (_unlimitedDurabilityHookInst == null)
                    {
                        if (_unlimitedDurabilityAddress != null)
                        {
                            var offset = _unlimitedDurabilityAddress[0];
                            _unlimitedDurabilityHookInst = _hook?.CreateHook("unlimitedDurability", offset);
                        }

                        _unlimitedDurabilityHookInst?.AddAsm("movaps xmm6,xmm0");
                        _unlimitedDurabilityHookInst?.AddJumpAsm("nop");
                        _unlimitedDurabilityHookInst?.AddJumpAsm("nop");
                    }

                    _unlimitedDurabilityHookInst?.Enable();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
                }
            }
            else
            {
                try
                {
                    if (_unlimitedDurabilityHookInst == null) return;
                    _unlimitedDurabilityHookInst.Disable();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"执行失败:{e.Message}");
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
                    if (_crash12Address == null)
                    {
                        await Task.Run(() =>
                        {
                            _crash12Address = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "engine_x64_rwdi.dll");
                        });
                    }

                    if (_crash12Address == null) return;
                    var baseAddress = _crash12Address[0];

                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "90 90 EB 05 B8 FF FF FF FF");
                }
                catch
                {
                    Console.WriteLine("功能开启失败。");
                }
            }
            else
            {
                try
                {
                    if (_crash12Address == null) return;
                    var baseAddress = _crash12Address[0];
                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "FF 12 EB 05 B8 FF FF FF FF");
                }
                catch
                {
                    Console.WriteLine("功能关闭失败。");
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
                    if (_immuneExplosionDmgAddress == null)
                    {
                        await Task.Run(() =>
                        {
                            _immuneExplosionDmgAddress = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    var baseAddress = _immuneExplosionDmgAddress[0];

                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "90 90 90 48 8B 5C 24 78");
                }
                catch
                {
                    Console.WriteLine("功能开启失败。");
                }
            }
            else
            {
                try
                {
                    var baseAddress = _immuneExplosionDmgAddress[0];
                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "FF 53 20 48 8B 5C 24 78");
                }
                catch
                {
                    Console.WriteLine("功能关闭失败。");
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
                    if (_crash5Address == null)
                    {
                        await Task.Run(() =>
                        {
                            _crash5Address = MemorySearcher.SearchInModule(
                                "DyingLightGame", pattern, "gamedll_x64_rwdi.dll");
                        });
                    }

                    var baseAddress = _crash5Address[0];

                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "90 90 90 90 48 89 93 98 00 00 00");
                }
                catch
                {
                    Console.WriteLine("功能开启失败。");
                }
            }
            else
            {
                try
                {
                    var baseAddress = _crash5Address[0];
                    MemoryWriter.WriteMemory("DyingLightGame", baseAddress, "48 8D 41 04 48 89 93 98 00 00 00");
                }
                catch
                {
                    Console.WriteLine("功能关闭失败。");
                }
            }
        }

        private void CardGroupItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("功能开发中，敬请期待。");
        }
    }
}