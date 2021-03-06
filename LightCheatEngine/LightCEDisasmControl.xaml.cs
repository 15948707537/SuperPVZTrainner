﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using SharpDisasm;
using PVZClass;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using SharpDisasm.Udis86;

namespace LightCheatEngine
{
    /// <summary>
    /// LightCEDisasmControl.xaml 的交互逻辑
    /// </summary>
    public partial class LightCEDisasmControl : UserControl
    {
        public LightCEDisasmControl()
        {
            InitializeComponent();
            LVMain.ItemsSource = List;
            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "可执行程序(*.exe)|*.exe";
            saveFileDialog.Title = "保存程序";
            saveFileDialog.DefaultExt = "exe";
        }
        SaveFileDialog saveFileDialog;

        private static readonly ObservableCollection<DisasmItem> List = new ObservableCollection<DisasmItem>();

        private void LVMain_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MenuItem_Click_1(null, null);
        }

        private void LVMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers== ModifierKeys.Control)
            {
                if (e.Key== Key.G)
                    MenuItem_Click(null, null);
                else if(e.Key == Key.S)
                    MenuItem_Click_6(null, null);
                else if (e.Key == Key.D)
                    MenuItem_Click_8(null, null);
            }
            else if (e.Key ==  Key.Space)
                MenuItem_Click_1(null, null);
            else if (e.Key == Key.Delete)
                MenuItem_Click_2(null, null);
            else if (e.Key == Key.F5)
                MenuItem_Click_7(null, null);
            else if (e.Key == Key.PageDown)
            {
                if (PVZ.Game != null)
                {
                    StartAddress += 80;
                    AddDisasmItem();
                }
            }
            else if (e.Key == Key.PageUp)
            {
                if (PVZ.Game != null)
                {
                    StartAddress -= 80;
                    AddDisasmItem();
                }
            }
            else if (e.Key == Key.Enter)
            {
                ContextMenu_Opened(null, null);
                if (MIFollow.Visibility == Visibility.Visible)
                    MenuItem_Click_9(null, null);
            }
            else if (e.Key== Key.Back)
            {
                ContextMenu_Opened(null, null);
                if (MIBack.Visibility == Visibility.Visible)
                    MIBack_Click(null, null);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (PVZ.Game != null)
            {
                StartAddress = PVZ.Game.MainModule.EntryPointAddress.ToInt32();
                AddDisasmItem();
            }
            else
            {
                List.Clear();
            }
            Window window = Window.GetWindow(this);
            window.Activated += Window_Activated;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (PVZ.Game != null)
                AddDisasmItem();
        }


        private void AddDisasmItem()
        {
            List.Clear();
            int offset = 0;
            while (!PVZ.Memory.CheckRead(StartAddress + offset))
            {
                List.Add(new DisasmItem(StartAddress + offset));
                offset++;
                if (offset == 256) return;
            }
            byte[] buffer = PVZ.Memory.ReadBytes(StartAddress + offset, 256 - offset);
            var disasm = new Disassembler(buffer, ArchitectureMode.x86_32, (ulong)StartAddress, true);
            foreach (var item in disasm.Disassemble())
            {
                List.Add(new DisasmItem(item));
            }
        }

        private double  ScrollPosition;

        private int StartAddress;

        private void LVMain_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollPosition = e.VerticalOffset;
        }

        private void LVMain_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PVZ.Game!=null)
            {
                if (e.Delta > 0 && ScrollPosition == 0)
                {
                    StartAddress -= 2;
                    AddDisasmItem();
                }
            }
        }

        Stack<int> privousAddress = new Stack<int>();

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (List.Count>0)
            {
                string result = DarkStyle.InputDialog.ShowInputDialog("转到地址", "输入要跳转到的地址", List[0].Address);
                if (result == null) return;
                if(symbols.ContainsKey(result))
                {
                    privousAddress.Push(StartAddress);
                    StartAddress = symbols[result];
                    AddDisasmItem();
                    LVMain.SelectedIndex = 0;
                    LVMain.ScrollIntoView(List[0]);
                }
                else
                {
                    try
                    {
                        privousAddress.Push(StartAddress);
                        if (result.StartsWith("0x") || result.StartsWith("0X"))
                            StartAddress = Convert.ToInt32(result.Substring(2), 16);
                        else
                            StartAddress = Convert.ToInt32(result, 16);
                        AddDisasmItem();
                        LVMain.SelectedIndex = 0;
                        LVMain.ScrollIntoView(List[0]);
                    }
                    catch
                    {
                        MessageBox.Show("地址不合法", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            if (LVMain.SelectedIndex >= 0)
            {
                int index = LVMain.SelectedIndex;
                DisasmItem disasmItem = List[index];
                string result = DarkStyle.InputDialog.ShowInputDialog($"汇编于{disasmItem.Address}", "输入汇编指令", disasmItem.AsmCode);
                if (result != null && result != "??")
                {
                    XEDPARSE xed = new XEDPARSE();
                    xed.cip = (ulong)disasmItem.address;
                    xed.instr = result;
                    XEDParse.XEDParseAssemble(ref xed);
                    if (xed.dest_size == 0)
                    {
                        MessageBox.Show(xed.error, "汇编指令错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        if (xed.dest_size == disasmItem.length)
                        {
                            PVZ.Memory.WriteBytesStrong(disasmItem.address, xed.dest, xed.dest_size);
                            AddDisasmItem();
                        }
                        else if (xed.dest_size < disasmItem.length)
                        {
                            PVZ.Memory.WriteBytesStrong(disasmItem.address, Enumerable.Repeat<byte>(0x90, disasmItem.length).ToArray(), disasmItem.length);
                            PVZ.Memory.WriteBytesStrong(disasmItem.address, xed.dest, xed.dest_size);
                            AddDisasmItem();
                        }
                        else if (xed.dest_size > disasmItem.length)
                        {
                            if (MessageBox.Show("目标指令长度大于当前指令长度，使用nop覆盖？", "提问", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                int length = disasmItem.length;
                                try
                                {
                                    while (xed.dest_size > length)
                                    {
                                        index++;
                                        length += List[index].length;
                                    }
                                }
                                catch (IndexOutOfRangeException)
                                {
                                    MessageBox.Show("指令长度未知", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }
                                PVZ.Memory.WriteBytesStrong(disasmItem.address, Enumerable.Repeat<byte>(0x90, length).ToArray(), length);
                                PVZ.Memory.WriteBytesStrong(disasmItem.address, xed.dest, xed.dest_size);
                                AddDisasmItem();
                            }
                        }
                    }
                }
            }
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            if (LVMain.SelectedIndex >= 0)
            {
                DisasmItem disasmItem = List[LVMain.SelectedIndex];
                PVZ.Memory.WriteBytesStrong(disasmItem.address, Enumerable.Repeat<byte>(0x90, disasmItem.length).ToArray(), disasmItem.length);
                AddDisasmItem();
            }
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            if (LVMain.SelectedIndex >= 0)
            {
                DisasmItem disasmItem = List[LVMain.SelectedIndex];
                Clipboard.SetText(disasmItem.Address);
            }
        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            if (LVMain.SelectedIndex >= 0)
            {
                DisasmItem disasmItem = List[LVMain.SelectedIndex];
                Clipboard.SetText(disasmItem.MachineCode);
            }
        }

        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            if (LVMain.SelectedIndex >= 0)
            {
                DisasmItem disasmItem = List[LVMain.SelectedIndex];
                Clipboard.SetText(disasmItem.AsmCode);
            }
        }

        private void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {
            if (PVZ.Game!=null)
            {
                saveFileDialog.FileName = PVZ.Game.ProcessName;
                if (saveFileDialog.ShowDialog() == true)
                {
                    ProcessModule module = PVZ.Game.MainModule;
                    using (FileStream stream = File.Create(saveFileDialog.FileName))
                    {
                        FileStream tempStream = File.OpenRead(module.FileName);
                        tempStream.CopyTo(stream);
                        tempStream.Close();
                        int pos = StartAddress - module.BaseAddress.ToInt32();
                        if (pos < 0 || pos > stream.Length)
                        {
                            MessageBox.Show("当前内存区域不在主模块内", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            stream.Seek(pos, SeekOrigin.Begin);
                            stream.Write(PVZ.Memory.ReadBytes(StartAddress, 256), 0, 256);
                        }
                    }
                }
            }
        }

        private void MenuItem_Click_7(object sender, RoutedEventArgs e)
        {
            if (LVMain.SelectedIndex >= 0)
            {
                DisasmItem disasmItem = List[LVMain.SelectedIndex];
                PVZ.Memory.CreateThread(disasmItem.address);
            }
        }


        Dictionary<string, int> symbols = new Dictionary<string, int>();

        private void MenuItem_Click_8(object sender, RoutedEventArgs e)
        {
            if (PVZ.Game != null)
            {
                string symbol = DarkStyle.InputDialog.ShowInputDialog("定义符号", "输入一个符号用于定位地址");
                if (symbol == null) return;
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    symbol = symbol.Trim();
                    if (symbols.ContainsKey(symbol))
                    {
                        if (MessageBox.Show($"符号{symbol}已经定义，确认要覆盖地址吗", "提问",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            symbols[symbol] = PVZ.Memory.AllocMemory();
                            privousAddress.Push(StartAddress);
                            StartAddress = symbols[symbol];
                            AddDisasmItem();
                            LVMain.SelectedIndex = 0;
                            LVMain.ScrollIntoView(List[0]);
                        }
                    }
                    else
                    {
                        symbols.Add(symbol, PVZ.Memory.AllocMemory());
                        privousAddress.Push(StartAddress);
                        StartAddress = symbols[symbol];
                        AddDisasmItem();
                        LVMain.SelectedIndex = 0;
                        LVMain.ScrollIntoView(List[0]);
                    }
                }
                else
                {
                    MessageBox.Show("符号无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuItem_Click_9(object sender, RoutedEventArgs e)
        {
            int address = (int)MIFollow.Tag;
            privousAddress.Push(StartAddress);
            StartAddress = address;
            AddDisasmItem();
            LVMain.SelectedIndex = 0;
            LVMain.ScrollIntoView(List[0]);
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (privousAddress.Count > 0)
            {
                MIBack.Header = "退回地址" + privousAddress.Peek().ToString("X8");
                MIBack.Visibility = Visibility.Visible;
            }
            else
            {
                MIBack.Visibility = Visibility.Collapsed;
            }
            if (LVMain.SelectedIndex >= 0)
            {
                DisasmItem disasmItem = List[LVMain.SelectedIndex];
                if (disasmItem.AsmCode.StartsWith("j") || disasmItem.AsmCode.StartsWith("call"))
                {
                    string[] subs = disasmItem.AsmCode.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (subs.Length == 2)
                    {
                        try
                        {
                            int address = Convert.ToInt32(subs[1].Trim().Substring(2), 16);
                            MIFollow.Header = "跟随地址" + address.ToString("X8");
                            MIFollow.Tag = address;
                            MIFollow.Visibility = Visibility.Visible;
                            return;
                        }
                        catch (FormatException)
                        {
                            MIFollow.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                }
                MIFollow.Visibility = Visibility.Collapsed;
            }
        }

        private void MIBack_Click(object sender, RoutedEventArgs e)
        {
            StartAddress = privousAddress.Pop();
            AddDisasmItem();
            LVMain.SelectedIndex = 0;
            LVMain.ScrollIntoView(List[0]);
        }
    }

    public class DisasmItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int address;
        public int length;
        private string mcode;
        private string asmCode;

        public string Address => address.ToString("X8");

        public string MachineCode
        {
            get => mcode;
            set
            {
                mcode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MachineCode"));
            }
        }

        public string AsmCode
        {
            get => asmCode;
            set 
            { 
                asmCode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AsmCode"));
            }
        }

        public Instruction instruction;

        public DisasmItem(Instruction instruction)
        {
            this.instruction = instruction;
            address = (int)instruction.Offset;
            length = instruction.Bytes.Length;
            mcode = Disassembler.Translator.TranslateBytes(instruction).ToUpper();
            asmCode = Disassembler.Translator.TranslateMnemonic(instruction);
        }

        public DisasmItem(int addr)
        {
            address = addr;
            length = 1;
            mcode = "??";
            asmCode = "??";
        }
    }
}
