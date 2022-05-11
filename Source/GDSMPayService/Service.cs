using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using PRT.GDSMPay.CMPP3.SMS;
using PRT.GDSMPay.Common;
using System.Threading;

namespace GDSMPayService
{
    public partial class Service : ServiceBase
    {
        #region 配置信息
        string SPID = "901234";
        string Password = "1234";
        string RouteAddress = "127.0.0.1";
        int Port = 7890;
        #endregion

        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: 在此处添加代码以启动服务。
            #region 接收短信线程
            Thread GetMessageThread = new Thread(GetMessage);
            GetMessageThread.Start();
            #endregion

            #region 发送短信线程
            Thread SendMessageThread = new Thread(SendMessage);
            SendMessageThread.Start();
            #endregion
        }

        protected override void OnStop()
        {
            // TODO: 在此处添加代码以执行停止服务所需的关闭操作。
        }

        #region 处理短信接收发送服务
        private void GetMessage()
        {
            CMPP30 m_CMPP;
            m_CMPP = new CMPP30(SPID, Password, RouteAddress, Port);
            m_CMPP.SMSStateChanged += new SMSEventHandler(OnCMPP);//定义事件处理函数
            m_CMPP.StartThread();
        }

        //移动短信网关事件（异步处理）
        protected void OnCMPP(Object sender, SMSEventArgs e)
        {
            SMSAsyncEvent ae = new SMSAsyncEvent(ProcessCMPPEvent);
            IAsyncResult ar = null;
            ar = ae.BeginInvoke(e, new AsyncCallback(CMPPAsyncEventCallBack), ae);
        }

        //异步事件处理函数
        private delegate void messageDelegate(string message);
        protected void ProcessCMPPEvent(SMSEventArgs e)
        {
            string strState = string.Empty;
            if (e.State == SMS_STATE.DELIVER)
            {
                CMPP_DELIVER deliver = (CMPP_DELIVER)e.Data;
                string sSql = string.Format("insert into message (msgid,pn,message)values('{0}','{1}','{2}')", deliver.Msg_ID, deliver.Src_Terminal_Id, deliver.Msg_Content);
                try {
                    OracleHelper.ExecSql(sSql);
                }
                catch (Exception ex)
                {
                    LocalLog.WriteLogFile(ex);
                    LocalLog.WriteMissMessage("-------------------------" +DateTime.Now.ToString()+ "-------------------------" );
                    LocalLog.WriteMissMessage("电话号码：" + deliver.Src_Terminal_Id);
                    LocalLog.WriteMissMessage("短信内容：" + deliver.Msg_Content);
                }
            }
            else if (e.State == SMS_STATE.DELIVER_RESPONSE)
            {
                CMPP_DELIVER_RESP resp = (CMPP_DELIVER_RESP)e.Data;

            }
            else if (e.State == SMS_STATE.REPORT)
            {
                CMPP_DELIVER deliver = (CMPP_DELIVER)e.Data;
                CMPP_REPORT report = deliver.GetReport();

            }
            else if (e.State == SMS_STATE.SUBMIT)
            {
                CMPP_SUBMIT submit = (CMPP_SUBMIT)e.Data;

            }
            else if (e.State == SMS_STATE.SUBMIT_RESPONSE)
            {
                CMPP_SUBMIT_RESP resp = (CMPP_SUBMIT_RESP)e.Data;

            }
            else if (e.State == SMS_STATE.ACTIVE_TEST)
            {

            }
            else if (e.State == SMS_STATE.ACTIVE_TEST_RESPONSE)
            {
                CMPP_ACTIVE_TEST_RESP resp = (CMPP_ACTIVE_TEST_RESP)e.Data;
            }
            else if (e.State == SMS_STATE.SP_CONNECT)
            {
                CMPP_CONNECT_RESP resp = (CMPP_CONNECT_RESP)e.Data;
            }
            else if (e.State == SMS_STATE.SP_DISCONNECT)
            {

            }
            else if (e.State == SMS_STATE.DELIVER_ERROR)
            {

            }
            else if (e.State == SMS_STATE.DELIVER_RESPONSE_ERROR)
            {

            }
            else if (e.State == SMS_STATE.SUBMIT_ERROR)
            {

            }
            else if (e.State == SMS_STATE.SUBMIT_RESPONSE_ERROR)
            {

            }
            else if (e.State == SMS_STATE.ACTIVE_TEST_ERROR)
            {

            }
            else if (e.State == SMS_STATE.ACTIVE_TEST_RESPONSE_ERROR)
            {

            }
            else if (e.State == SMS_STATE.SP_CONNECT_ERROR)
            {

            }
            else if (e.State == SMS_STATE.SP_DISCONNECT_ERROR)
            {

            }

        }

        //异步事件回调函数
        private void CMPPAsyncEventCallBack(IAsyncResult ar)
        {
            SMSAsyncEvent ae = (SMSAsyncEvent)ar.AsyncState;
            ae.EndInvoke(ar);
        }

        //异步发送回调函数
        private void CMPPAsyncSubmitCallBack(IAsyncResult ar)
        {
            CMPPAsyncSubmit s = (CMPPAsyncSubmit)ar.AsyncState;
            s.EndInvoke(ar);
        }
        #endregion

        #region 处理发送短消息
        private void SendMessage()
        {
            while (true)
            {
                CMPP30 m_CMPP;
                m_CMPP = new CMPP30(SPID, Password, RouteAddress, Port);
                m_CMPP.SMSStateChanged += new SMSEventHandler(OnCMPP);//定义事件处理函数
                m_CMPP.StartThread();
                string[] pns = { "13891923630" };
                m_CMPP.Submit("我是定时发送的短消息！！！", pns, "", "", CODING.GBK, true, 1, 1, 1, "", "");
                m_CMPP.EndThread();
                Thread.Sleep(10000); //5秒钟执行一次发送短消息操作
            }
        }
        #endregion
    }
}
