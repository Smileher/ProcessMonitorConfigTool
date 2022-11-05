using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ProcessMonitorConfigTool
{
    internal class MainWindowVM : ObservableRecipient
    {
        public ObservableCollection<ProcessInfo> ProcessInfos { get; set; }
        public ProcessConfig ProcessConfig { get; set; }
        private Process[] ProcessNow { get; set; }
        private List<string> ProcessNames { get; set; }
        private EditWindowVM _editWindowVM;
        public string XmlPath { get => Path.Combine(Environment.CurrentDirectory, "ConfigTool.xml"); }
        private ProcessInfo _selectedProcessInfo;
        public ProcessInfo SelectedProcessInfo
        {
            get => _selectedProcessInfo;
            set => SetProperty(ref _selectedProcessInfo, value);
        }
        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private bool _isRun = false;
        public bool IsRun
        {
            get => _isRun;
            set
            {
                SetProperty(ref _isRun, value);
                ProgressContent = value ? "" : "一键重新启动";
            }
        }
        private string _progressContent = "一键重新启动";
        public string ProgressContent
        {
            get => _progressContent;
            set => SetProperty(ref _progressContent, value);
        }
        public IRelayCommand OneKeyStartCommand { get; }
        public IRelayCommand StartCommand { get; }
        public IRelayCommand SaveConfigCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IRelayCommand NewCommand { get; }
        public IRelayCommand DeleteCommand { get; }
        public IRelayCommand MoveUpCommand { get; }
        public IRelayCommand MoveDownCommand { get; }
        public IRelayCommand CloseCommand { get; }
        public IRelayCommand CloseWinCommand { get; }
        static event EventHandler<AggregateException> AggregateExceptionCatched;
        public MainWindowVM()
        {
            AggregateExceptionCatched += new EventHandler<AggregateException>(Start);
            OneKeyStartCommand = new RelayCommand(OneKeyStart);
            StartCommand = new RelayCommand(() => { Start(); });
            CloseCommand = new RelayCommand(Close);
            EditCommand = new RelayCommand(Edit);
            NewCommand = new RelayCommand(New);
            SaveConfigCommand = new RelayCommand(SaveConfig);
            DeleteCommand = new RelayCommand(Delete);
            MoveUpCommand = new RelayCommand(MoveUp);
            MoveDownCommand = new RelayCommand(MoveDown);
            CloseWinCommand = new RelayCommand(CloseWin);
            Init();
        }

        private void OneKeyStart()
        {
            try
            {
                if (ProcessConfig.ProcessInfo.Count() <= 0)
                {
                    throw new Exception("服务数量为空!");
                }
                IsRun = true;
                ProcessInfos.ForEach(ProcessInfo =>
                {
                    SelectedProcessInfo = ProcessInfo;
                    Close();
                    Start();
                    Task.Run(async () =>
                    {
                        Start();
                        await Task.Delay((int)SelectedProcessInfo.TimeOut);
                        if (ProgressValue <= 100)
                        {
                            ProgressValue += 100 / ProcessInfos.Count;
                        }
                    });
                    //Thread.Sleep((int)SelectedProcessInfo.TimeOut);
                    //Task.Run(async () =>
                    //{
                    //    Start();
                    //    await Task.Delay((int)SelectedProcessInfo.TimeOut);
                    //});
                });
                IsRun = false;
            }
            catch (Exception ex)
            {
                IsRun = false;
                Growl.ErrorGlobal(ex.Message);
            }
        }

        private void OneKeyStartThread()
        {
            //try
            //{
            //    //var time = ProcessInfos.Sum(x => x.TimeOut + 500) - ProcessInfos.Last().TimeOut;
            //    Stopwatch stopWatch = new Stopwatch();
            //    stopWatch.Start();
            //    ProgressValue = 0;
            //    ProcessInfos.ForEach(ProcessInfo =>
            //    {
            //        SelectedProcessInfo = ProcessInfo;
            //        Close();
            //        Start();
            //        //if (!ProcessNames.Contains(ProcessInfo.ProcessName))
            //        //{
            //        //    IsRun = false;
            //        //    ProgressValue = 0;
            //        //    throw new Exception(SelectedProcessInfo.DisplayName + " 启动失败！");
            //        //}
            //        Task.Delay((int)SelectedProcessInfo.TimeOut);
            //        if (ProgressValue <= 100)
            //        {
            //            ProgressValue += 100 / ProcessInfos.Count;
            //        }
            //        //if (ProcessInfo != ProcessInfos.Last())
            //        //{
            //        //    Thread.Sleep((int)SelectedProcessInfo.TimeOut);
            //        //}
            //        //Thread.Sleep(500);
            //        //if (!ProcessNames.Contains(ProcessInfo.ProcessName))
            //        //{
            //        //    IsRun = false;
            //        //}
            //        //else
            //        //{
            //        //}
            //    });
            //    IsRun = false;
            //}
            //catch (Exception)
            //{
            //    IsRun = false;
            //    ProgressValue = 0;
            //    Growl.ErrorGlobal("一键启动失败！");
            //}
        }
        private void SaveConfig()
        {
            try
            {
                ObjectToXmlFile(ProcessConfig, XmlPath);
                Growl.SuccessGlobal("保存成功!");
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal("保存失败！" + ex.Message);
            }
        }

        private void Init()
        {
            try
            {
                if (!File.Exists(XmlPath))
                {
                    throw new Exception();
                }
                ProcessConfig = XmlFileToObject<ProcessConfig>(XmlPath);
                ProcessInfos = ProcessConfig.ProcessInfo;
            }
            catch (Exception)
            {
                var result = MessageBox.Show(Path.GetFileName(XmlPath) + "配置文件读取异常！是否启动？", "消息提醒", MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (result == MessageBoxResult.No)
                {
                    Environment.Exit(0);
                }
                CreateDefaultData();
            }
            finally
            {
                ProcessInfos = ProcessConfig.ProcessInfo;
                Task.Run(async () =>
                {
                    while (true)
                    {
                        ProcessNow = Process.GetProcesses();
                        ProcessNames = ProcessNow.Select(x => x.ProcessName).ToList();
                        ProcessInfos.ForEach(ProcessInfo =>
                        {
                            if (!string.IsNullOrEmpty(ProcessInfo.ProcessName))
                            {
                                if (ProcessNames.Contains(ProcessInfo.ProcessName))
                                {
                                    ProcessInfo.State = Brushes.Green;
                                }
                                else
                                {
                                    ProcessInfo.State = Brushes.Red;
                                }
                            }
                            else
                            {
                                ProcessInfo.State = Brushes.Gray;
                            }
                        });
                        await Task.Delay(100);
                    }
                });
            }
        }
        private void CreateDefaultData()
        {
            ProcessConfig = new ProcessConfig
            {
                ProcessInfo = new ObservableCollection<ProcessInfo>()
            };
        }

        private void Close()
        {
            try
            {
                if (!string.IsNullOrEmpty(SelectedProcessInfo?.Parm) && !string.IsNullOrEmpty(SelectedProcessInfo?.ProcessName) && !string.IsNullOrEmpty(SelectedProcessInfo?.DisplayName))
                {
                    var ProcessKill = ProcessNow.Where(p => p.ProcessName == SelectedProcessInfo.ProcessName).ToList();

                    ProcessKill.ForEach(x => x.Kill());
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void CloseWin()
        {
            Growl.ClearGlobal();
            Environment.Exit(0);
        }
        private void MoveUp()
        {
            if (SelectedProcessInfo != null)
            {
                int index = ProcessInfos.IndexOf(SelectedProcessInfo);
                if (index > 0)
                {
                    ProcessInfos.Move(index, index - 1);
                }
            }
        }
        private void MoveDown()
        {
            if (SelectedProcessInfo != null)
            {
                int index = ProcessInfos.IndexOf(SelectedProcessInfo);
                if (index + 1 < ProcessInfos.Count())
                {
                    ProcessInfos.Move(index, index + 1);
                }
            }
        }

        private void Delete()
        {
            if (SelectedProcessInfo != null)
            {
                var index = ProcessInfos.IndexOf(SelectedProcessInfo);
                ProcessInfos.Remove(SelectedProcessInfo);
                SelectedProcessInfo = ProcessInfos.ElementAtOrDefault(index) ?? null;
                SelectedProcessInfo = SelectedProcessInfo ?? ProcessInfos.LastOrDefault();
            }
        }
        private void New()
        {
            var NewProcessInfo = new ProcessInfo();
            _editWindowVM = new EditWindowVM(NewProcessInfo);
            WindowManager.Show("EditWindow", _editWindowVM);
            if (!string.IsNullOrEmpty(NewProcessInfo.DisplayName) && !string.IsNullOrEmpty(NewProcessInfo.ProcessName) && !string.IsNullOrEmpty(NewProcessInfo.Parm))
            {
                ProcessConfig.ProcessInfo.Add(NewProcessInfo);
            }
        }

        private void Edit()
        {
            _editWindowVM = new EditWindowVM(SelectedProcessInfo);
            WindowManager.Show("EditWindow", _editWindowVM);
        }
        private void Start(object sender = null, AggregateException e = null)
        {
            if (e != null)
            {
                Growl.ErrorGlobal("服务：[" + SelectedProcessInfo.DisplayName + "]启动失败！原因：" + e.Message);
                return;
            }
            if (string.IsNullOrEmpty(SelectedProcessInfo?.Parm) || string.IsNullOrEmpty(SelectedProcessInfo?.ProcessName) || string.IsNullOrEmpty(SelectedProcessInfo?.DisplayName))
            {
                return;
            }
            switch (SelectedProcessInfo.Type)
            {
                case ParmType.EXE:
                    {
                        StartExe(SelectedProcessInfo.Parm);
                    }
                    break;
                case ParmType.BAT:
                    {
                        StartBat(SelectedProcessInfo.Parm);
                    }
                    break;
                case ParmType.CMD:
                    {
                        StartCmd(SelectedProcessInfo.Parm);
                    }
                    break;
                default:
                    break;
            }
        }
        private Task StartExe(string parm)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(parm))
                    {
                        throw new AggregateException("系统找不到指定的文件。");
                    }
                    Process p = Process.Start(parm);
                    p.Close();
                }
                catch (AggregateException e)
                {
                    //使用主线程委托代理，处理子线程 异常,这种方式没有阻塞 主线程或其他线程
                    AggregateExceptionCatched?.Invoke(null, e);
                }
            });
        }
        private Task StartBat(string parm)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(parm))
                    {
                        throw new AggregateException("系统找不到指定的文件。");
                    }
                    Process p = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = parm,
                            UseShellExecute = false,//是否使用操作系统shell启动
                            RedirectStandardInput = true,//接受来自调用程序的输入信息
                            RedirectStandardOutput = true,//输出信息
                            RedirectStandardError = true,// 输出错误
                            CreateNoWindow = true//不显示程序窗口
                        }
                    };
                    p.Start();
                    //获取输出信息
                    string strOuput = p.StandardOutput.ReadToEnd();
                    string strErrOuput = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(strErrOuput))
                    {
                        throw new AggregateException(strErrOuput);
                    }
                    p.Close();
                }
                catch (AggregateException e)
                {
                    //使用主线程委托代理，处理子线程 异常,这种方式没有阻塞 主线程或其他线程
                    AggregateExceptionCatched?.Invoke(null, e);
                }
            });
        }
        private Task StartCmd(string parm)
        {
            return Task.Run(() =>
            {
                try
                {
                    Process p = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "cmd.exe",//设置要启动的应用程序
                            UseShellExecute = false,//是否使用操作系统shell启动
                            RedirectStandardInput = true,//接受来自调用程序的输入信息
                            RedirectStandardOutput = true,//输出信息
                            RedirectStandardError = true,// 输出错误
                            CreateNoWindow = true//不显示程序窗口
                        }
                    };
                    //启动程序
                    p.Start();
                    //向cmd窗口发送输入信息
                    p.StandardInput.WriteLine(parm + "&exit");
                    p.StandardInput.AutoFlush = true;
                    //获取输出信息
                    string strOuput = p.StandardOutput.ReadToEnd();
                    string strErrOuput = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(strErrOuput))
                    {
                        throw new AggregateException(strErrOuput);
                    }
                }
                catch (AggregateException e)
                {
                    //使用主线程委托代理，处理子线程 异常,这种方式没有阻塞 主线程或其他线程
                    AggregateExceptionCatched?.Invoke(null, e);
                }
            });
        }
        /// <summary>
        /// 将xml文件反序列化为对象
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="t">对象</param>
        /// <param name="filePathAndName">xml文件的路径和名称</param>
        /// <returns>对象</returns>
        public static T XmlFileToObject<T>(string filePathAndName) where T : class
        {
            if (string.IsNullOrEmpty(filePathAndName)) return null;

            try
            {
                XmlSerializer formatter = new XmlSerializer(typeof(T));
                using (FileStream stream = new FileStream(filePathAndName, FileMode.OpenOrCreate))
                {
                    XmlReader xmlReader = new XmlTextReader(stream);
                    T result = formatter.Deserialize(xmlReader) as T;
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("配置文件错误！" + ex.Message);
            }
        }
        /// <summary>
        /// 将对象序列化为xml文件且保存
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="t">对象</param>
        /// <param name="filePathAndName">xml存放路径和名称</param>
        /// <returns>True:表示成功</returns>
        public static bool ObjectToXmlFile<T>(T t, string filePathAndName) where T : class
        {
            if (t == null || string.IsNullOrEmpty(filePathAndName)) return false;

            bool success = false;
            try
            {
                if (File.Exists(filePathAndName))
                {
                    File.Delete(filePathAndName);
                }
                XmlSerializer formatter = new XmlSerializer(typeof(T));
                using (FileStream stream = new FileStream(filePathAndName, FileMode.OpenOrCreate))
                {
                    XmlSerializerNamespaces _namespaces = new XmlSerializerNamespaces(
                    new XmlQualifiedName[] {
                        new XmlQualifiedName(string.Empty, "")
                 });
                    formatter.Serialize(stream, t, _namespaces);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return success;
        }
    }
    public class ProcessConfig : ObservableObject
    {
        [XmlElement("ProcessInfo")]
        public ObservableCollection<ProcessInfo> ProcessInfo { get; set; }
    }
    public class ProcessInfo : ObservableObject
    {
        private string _displayName;
        private string _processName;
        private uint _timeOut;
        private ParmType _type;
        private string _parm;
        private SolidColorBrush _state;

        [XmlAttribute("DisplayName")]
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        [XmlAttribute("ProcessName")]
        public string ProcessName
        {
            get => _processName;
            set => SetProperty(ref _processName, value);
        }

        [XmlAttribute("Type")]
        public ParmType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        [XmlAttribute("Parm")]
        public string Parm
        {
            get => _parm;
            set => SetProperty(ref _parm, value);
        }
        [XmlAttribute("TimeOut")]
        public uint TimeOut
        {
            get => _timeOut;
            set => SetProperty(ref _timeOut, value);
        }
        [XmlIgnore]
        public SolidColorBrush State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }
    }
    public enum ParmType
    {
        [Description("exe格式程序")]
        EXE = 0,
        [Description("bat格式脚本")]
        BAT = 1,
        [Description("命令行参数")]
        CMD = 2
    }
}
