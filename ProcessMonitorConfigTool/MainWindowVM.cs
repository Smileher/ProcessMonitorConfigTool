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
        private DispatcherTimer _timer;

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

        public MainWindowVM()
        {
            OneKeyStartCommand = new RelayCommand(OneKeyStart);
            StartCommand = new RelayCommand(Start);
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
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                new Thread(OneKeyStartThread)
                {
                    IsBackground = true
                }.Start();
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
            }
        }

        private void OneKeyStartThread()
        {
            try
            {
                //var time = ProcessInfos.Sum(x => x.TimeOut + 500) - ProcessInfos.Last().TimeOut;
                var Timer = new DispatcherTimer();
                ProgressValue = 0;
                ProcessInfos.ForEach(ProcessInfo =>
                {
                    SelectedProcessInfo = ProcessInfo;
                    Close();
                    Task t = new Task(() => {
                        Start();
                    });
                    t.Start();
                    if (!t.Wait((int)SelectedProcessInfo.TimeOut))
                    {
                        
                    }

                    if (ProcessInfo != ProcessInfos.Last())
                    {
                        Thread.Sleep((int)SelectedProcessInfo.TimeOut);
                    }
                    Thread.Sleep(500);
                    if (!ProcessNames.Contains(ProcessInfo.ProcessName))
                    {
                        IsRun = false;
                    }
                    else
                    {
                        if (ProgressValue <= 100)
                        {
                            ProgressValue += 100 / ProcessInfos.Count;
                        }
                    }
                });
                IsRun = false;
            }
            catch (Exception)
            {
                throw new Exception(SelectedProcessInfo.DisplayName + " 启动失败！");
            }
        }
        private void SaveConfig()
        {
            try
            {
                ObjectToXmlFile(ProcessConfig, XmlPath);
                Growl.Success("保存成功!");
            }
            catch (Exception ex)
            {
                Growl.Error("保存失败！" + ex.Message);
            }
        }

        private void Init()
        {
            try
            {
                if (!File.Exists(XmlPath))
                {
                    throw new NotImplementedException();
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
                new Thread(ProcessStateThread)
                {
                    IsBackground = true
                }.Start();

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
            //try
            //{
            //    ObjectToXmlFile(ProcessConfig, XmlPath);
            //}
            //catch (Exception)
            //{

            //    throw;
            //}
        }

        internal static T TimeoutCheck<T>(int ms, Func<T> func)
        {
            var wait = new ManualResetEvent(false);
            bool RunOK = false;
            var task = Task.Run<T>(() =>
            {
                var result = func.Invoke();
                RunOK = true;
                wait.Set();
                return result;
            });
            wait.WaitOne(ms);
            if (RunOK)
            {
                return task.Result;
            }
            else
            {
                return default(T);
            }
        }

        private void ProcessStateThread()
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
                Thread.Sleep(50);
            }
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
        private void Start()
        {
            try
            {
                if (!string.IsNullOrEmpty(SelectedProcessInfo?.Parm) && !string.IsNullOrEmpty(SelectedProcessInfo?.ProcessName) && !string.IsNullOrEmpty(SelectedProcessInfo?.DisplayName))
                {
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
            }
            catch (Exception ex)
            {
                Growl.Error("服务：[" + SelectedProcessInfo.DisplayName + "]启动失败！" + ex.Message);
            }
        }
        private void StartExe(string parm)
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = parm;
                p.StartInfo.Verb = "Open";
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.Close();
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void StartBat(string parm)
        {
            try
            {
                if (!File.Exists(parm))
                {
                    throw new Exception("系统找不到指定的文件。");
                }
                Process p = new Process();
                p.StartInfo.FileName = parm;
                p.StartInfo.UseShellExecute = false;//运行时隐藏dos窗口
                p.StartInfo.CreateNoWindow = true;//运行时隐藏dos窗口
                p.StartInfo.Verb = "runas";//设置该启动动作，会以管理员权限运行进程
                p.Start();
                p.Close();
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void StartCmd(string Input)
        {
            try
            {
                Process p = new Process();
                //设置要启动的应用程序
                p.StartInfo.FileName = "cmd.exe";
                //是否使用操作系统shell启动
                p.StartInfo.UseShellExecute = false;
                //设置该启动动作，会以管理员权限运行进程
                p.StartInfo.Verb = "runas";
                // 接受来自调用程序的输入信息
                p.StartInfo.RedirectStandardInput = true;
                //输出信息
                p.StartInfo.RedirectStandardOutput = true;
                // 输出错误
                p.StartInfo.RedirectStandardError = true;
                //不显示程序窗口
                p.StartInfo.CreateNoWindow = true; 
                //启动程序
                p.Start();
                //向cmd窗口发送输入信息
                p.StandardInput.WriteLine(Input + "&exit");
                p.StandardInput.AutoFlush = true;
                //获取输出信息
                string strOuput = p.StandardOutput.ReadToEnd();
                string strErrOuput = p.StandardError.ReadToEnd();
                p.WaitForExit();
                p.Close();
                if (!string.IsNullOrEmpty(strErrOuput))
                {
                    //等待程序执行完退出进程
                    throw new Exception(strErrOuput);
                }
            }
            catch (Exception)
            {
                throw;
            }
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
