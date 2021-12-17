using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using WeifenLuo.WinFormsUI.Docking;
using LogRecord;
using LeoCam;
using HalconDotNet;
using System.Runtime.InteropServices;
using LeoMotion;
using System.Runtime.Serialization.Formatters.Binary;

namespace SZ_BydKeyboard
{
    public partial class MainForm : DevComponents.DotNetBar.Metro.MetroAppForm
    {
        public MainForm()
        {
            InitializeComponent();
        }

        //��ȡ�ؼ�����
        private IDockContent GetContentFromPersistString(string persistString)
        {
            try
            {
                if (persistString == typeof(FrmLogger).ToString())
                {
                    return Common.frmLog;
                }
                if (persistString == typeof(FrmShow).ToString())
                {
                    return Common.frmShow;
                }
                //if (persistString == typeof(FrmMesDataSetting).ToString())
                //{
                //    return Common.frmMESSetting;
                //}
                //if (persistString == typeof(FrmStatistics).ToString())
                //{
                //    return Common.frmStatistics;
                //}
            }
            catch (Exception ex)
            {
                LogHelper.Exception(this.GetType(), ex);

            }
            return null;
        }

        //��ʼ������
        private void InitMainformLayout()
        {
            if (File.Exists(Path.Combine(Application.StartupPath, "SysCfg\\CustomUI.xml")))
            {
                DeserializeDockContent dd = new DeserializeDockContent(GetContentFromPersistString);
                MainPanel.LoadFromXml(Path.Combine(Application.StartupPath, "SysCfg\\CustomUI.xml"), dd);
            }
            else
            {
                Common.frmLog.Show(this.MainPanel, DockState.DockRight);
                Common.frmShow.Show(this.MainPanel, DockState.Document);
            }
        }

        private void InitCameraAndLMI3D()
        {
            try
            {
                Common.Cam1 = new ECamera();
                if (Common.Cam1.Open("CAM1"))
                {
                    Common.ShowMsgEvent($"Tips:�����1�ɹ�!");
                    Common.Cam1.grabFinishCall = Common.grabImage1;
                }
                else
                {
                    Common.ShowMsgEvent($"Error:�����1ʧ��!");
                }
                Common.Cam2 = new ECamera();
                if (Common.Cam2.Open("CAM2"))
                {
                    Common.ShowMsgEvent($"Tips:�����2�ɹ�!");
                    Common.Cam2.grabFinishCall = Common.grabImage2;
                }
                else
                {
                    Common.ShowMsgEvent($"Error:�����2ʧ��!");
                }
            }
            catch (Exception EX)
            {
                Common.ShowMsgEvent($"Error:{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}{EX.Message}");
                LogHelper.Exception(this.GetType(), EX);
            }
        }

        public void LoadParam()
        {
            string fp = $"{System.Windows.Forms.Application.StartupPath}\\Products\\{Common.str_ProductName}\\SysCfg\\RunningData.json";
            if (File.Exists(fp))
            {
                Common.Data = JsonConvert.DeserializeObject<RunningData>(File.ReadAllText(fp));
            }
            else
            {
                Common.Data = new RunningData();
            }

            try
            {

                FileStream fs = new FileStream($"{System.Windows.Forms.Application.StartupPath}\\Products\\{Common.str_ProductName}\\SysCfg\\RunningData.hol", FileMode.Open, FileAccess.Read);
                using (fs)
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    Common.HalObject = (HalconObject)bf.Deserialize(fs);
                    if (Common.HalObject.FirstPinCalibra == null)
                    {
                        Common.HalObject.FirstPinCalibra = new Leo9Calibra();
                    }
                    if (Common.HalObject.SecondPinCalibra == null)
                    {
                        Common.HalObject.SecondPinCalibra = new Leo9Calibra();
                    }
                }
            }
            catch (Exception ex)
            {
                Common.ShowMsgEvent($"Error:��ȡ����ʧ�ܣ�ԭ��Ϊ{ex.Message}!");
                Common.HalObject = new HalconObject();
                Common.HalObject.ho_FocusRegion.GenEmptyObj();
            }
        }




        private void MainForm_Load(object sender, EventArgs e)
        {
            HOperatorSet.SetSystem("clip_region", "false");
            BackWorker.WorkerSupportsCancellation = true;
            BackWorker.WorkerReportsProgress = true;

            InitMainformLayout();

            Thread.Sleep(500);
            InitCameraAndLMI3D();
            LoadParam();
            OmronPLC.GetInstance().Init_TCP("192.168.88.100", 502);

            Common.ProC.LoadParam();
            Common.ProC = ProControl.GetInstance();

            Common.ProC.InitParam();
            Common.frmMotionSetting.LoadMotionData();

        }

        private void Btn_SaveLayout_Click(object sender, EventArgs e)
        {
            if (File.Exists(Path.Combine(Application.StartupPath, "SysCfg\\CustomUI.xml")))
            {
                File.Delete(Path.Combine(Application.StartupPath, "SysCfg\\CustomUI.xml"));
            }
            MainPanel.SaveAsXml(Path.Combine(Application.StartupPath, "SysCfg\\CustomUI.xml"));
        }

        private void buttonX3_Click(object sender, EventArgs e)
        {
            Common.frmSetting.Timer.Enabled = true;
            Common.frmSetting.Show();
            Common.frmSetting.BringToFront();
        }

        private void Btn_GoHome_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == MessageBox.Show("��ȷ���豸���ڰ�ȫ״̬���һ�ԭ·���޸�����ײ�����ٻ�ԭ",
                 "��ʾ��Ϣ ", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2))
            {
                Btn_GoHome.Enabled = false;
                if (Common.ProC != null)
                {
                    Common.ProC.AllAxisGohome();
                }
                Btn_GoHome.Enabled = true;
            }
        }

        private void BTN_QuitSystem_Click(object sender, EventArgs e)
        {
            Common.ProC.SaveParam();
            if (Common.Cam1 != null ? Common.Cam1.Status == CamStatus.Open : false)
            {
                if (Common.Cam1.IsStart)
                {
                    Common.Cam1.Stop();
                }
                Common.Cam1.Close();
            }

            if (Common.Cam2 != null ? Common.Cam2.Status == CamStatus.Open : false)
            {
                if (Common.Cam2.IsStart)
                {
                    Common.Cam2.Stop();
                }
                Common.Cam2.Close();
            }
            Common.frmMotionSetting.Dispose();
            Common.frmSetting.Dispose();
            Application.Exit();
        }

        private void BTN_AutoRun_Click(object sender, EventArgs e)
        {
            //���Ļص�������
            Common.Cam1.grabFinishCall = Common.grabImage1;
            Common.Cam2.grabFinishCall = Common.grabImage2;
            BTN_AutoRun.Enabled = false;
            BTN_Pause.Enabled = true;
            BackWorker.RunWorkerAsync();
        }

        private void BTN_Pause_Click(object sender, EventArgs e)
        {
            BTN_AutoRun.Enabled = true;
            BTN_Pause.Enabled = false;
            BackWorker.CancelAsync();
        }

        public bool b_Start = false, b_Reset = false, b_HardStop = false;

        private void BackWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (BackWorker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                Thread.Sleep(20);
                Common.ProC.GetStatus();
                Common.ProC.Control_T();
                int value1 = OmronPLC.GetInstance().GetAddressValue("10280");
                int value2 = OmronPLC.GetInstance().GetAddressValue("10281");

                if (value1 == 1 || value1 == 2)
                {
                    Common.ShowMsgEvent.Invoke("Tips:" + DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms") + " -->�յ�ƽ̨��ԭ�ź�--");
                    OmronPLC.GetInstance().Send("10280", 0);
                    Common.ProC.AllAxisGohome();
                    OmronPLC.GetInstance().Send("10280", 31);
                }
                else if (value1 == 3)
                {
                    Common.ShowMsgEvent.Invoke("Tips:" + DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms") + " -->�յ�оƬ��λ�ź�--");
                    OmronPLC.GetInstance().Send("10280", 0);
                    Common.frmShow.nMatch = 0;
                    Common.frmShow.b_First = true;
                    Common.frmShow.b_Match = true;
                }
                if (value2 == 1)
                {
                    Common.ShowMsgEvent.Invoke("Tips:" + DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms") + " -->�յ����Z��ȫ�ź�--");
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.frmShow.b_Zsafe = true;
                    Common.frmShow.nZsafe = 0;
                }
                else if (value2 == 2)
                {
                    Common.ShowMsgEvent.Invoke("Tips:" + DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms") + " -->�յ�����Խ��ź�--");
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.Data.Gradient = new List<double>();
                    Common.Data.PosList = new List<double>();
                    Common.frmShow.b_Focus = true;
                    Common.frmShow.nFocusStep = 0;
                    Common.frmShow.nImageIndex = 0;
                }
                else if (value2 == 5)
                {
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.ShowMsgEvent.Invoke($"Tips:{ DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms")} -->�յ�ȡ�»�׼1�ź�--");
                    Common.frmShow.nGetNow1 = 0;
                    Common.frmShow.b_GetNow1 = true;
                }
                else if (value2 == 6)
                {
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.ShowMsgEvent.Invoke($"Tips:{ DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms")} -->�յ�ȡ�»�׼2�ź�--");
                    Common.frmShow.nGetNow2 = 0;
                    Common.frmShow.b_GetNow2 = true;
                }
                else if (value2 == 7)
                {
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.ShowMsgEvent.Invoke($"Tips:{ DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms")} -->�յ�ȡ��׼1�ź�--");
                    Common.frmShow.nGetBase1 = 0;
                    Common.frmShow.b_GetBase1Finish = false;

                }
                else if (value2 == 8)
                {
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.ShowMsgEvent.Invoke($"Tips:{ DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms")} -->�յ�ȡ��׼2�ź�--");
                    Common.frmShow.nGetBase2 = 0;
                    Common.frmShow.b_GetBase2Finish = false;

                }
                else if (value2 >= 10 && value2 < 19)
                {
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.frmShow.nPinFirstIndex = value2 - 10;
                    Common.ShowMsgEvent.Invoke($"Tips:{ DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms")} -->�յ��뿨����궨1�źţ���ǰ�궨��λ[{Common.frmShow.nPinFirstIndex}]--");
                    Common.frmShow.nPinFirstStep = 0;
                    Common.frmShow.b_GetFirstPinPointsFinish = false;
                }
                else if (value2 >= 50 && value2 < 59)
                {
                    OmronPLC.GetInstance().Send("10281", 0);
                    Common.frmShow.nPinSecondIndex = value2 - 50;
                    Common.ShowMsgEvent.Invoke($"Tips:{ DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms")} -->�յ��뿨����궨2�źţ���ǰ�궨��λ[{Common.frmShow.nPinSecondIndex}]--");
                    Common.frmShow.nPinSecondStep = 0;
                    Common.frmShow.b_GetSecondPinPointsFinish = false;
                }

                //��ȡ�µ��뿨
                Common.frmShow.AutoGetNow1();
                Common.frmShow.AutoGetNow2();
                //�Զ�����׼
                Common.frmShow.AutoGetBase1();
                Common.frmShow.AutoGetBase2();
                //�Զ��궨1�뿨���
                Common.frmShow.AutoFirstPinCalibra(Common.frmShow.nPinFirstIndex);
                //�Զ��궨2�뿨���
                Common.frmShow.AutoSecondPinCalibra(Common.frmShow.nPinSecondIndex);

                //�Զ��Խ�
                if (Common.frmShow.b_Focus)
                {
                    Common.frmShow.AutoFocus();
                    Common.frmShow.FocusAndSaveImage();
                }
                //Z�ᰲȫ
                if (Common.frmShow.b_Zsafe)
                {
                    Common.frmShow.AutoGoSafeZ();
                }
                //�Զ���λ
                if (Common.frmShow.b_Match)
                {
                    Common.frmShow.AutoMatch();
                }
            }
        }


        private async void buttonX2_Click(object sender, EventArgs e)
        {
            Common.ProC.CancelCardSoftLmt();
            List<Task<bool>> taskList = new List<Task<bool>>();
            //��1
            for (int i = 0; i < Common.ProC.Card1.AxisName.Length; i++)
            {
                int axis = i;
                taskList.Add(Task<bool>.Run(() =>
                {
                    return Common.ProC.Card1.GoHome(axis, 0, 1, 2, 0); //�������
                }));
                Thread.Sleep(20);
            }
            var b = await Task.WhenAll(taskList);
            Common.ShowMsgEvent.Invoke("Tips:" + DateTime.Now.ToString("----yyyy/MM/dd hh:mm:ss:ms -->") + string.Format("�������ԭ���--"));
            Common.ProC.SetCardSoftLmt();
        }

        private void Btn_PositionSetting_Click(object sender, EventArgs e)
        {

        }

        private void buttonX4_Click(object sender, EventArgs e)
        {
           
        }

        private void buttonX1_Click(object sender, EventArgs e)
        {

        }

        private void Btn_MotionSetting_Click(object sender, EventArgs e)
        {
            Common.frmMotionSetting.cardControl1.Timer.Enabled = true;
            Common.frmMotionSetting.Show();
            Common.frmMotionSetting.BringToFront();
        }
    }
}