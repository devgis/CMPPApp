/*这是2005年6月云南移动短信网关升级到3.0时写的，在SP那稳定运行了很长时间的。因为SP倒闭了，贴出来给有兴趣的朋友参考。
优点：支持多线程、滑动窗口、异步发送、全事件模式、自动识别ASCII、GBK、UCS-2
缺点：不支持长短信自动分页、不支持PROVISION接口（偶的PROVISION接口是用WEB SERVICE实现的）*/

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace PRT.GDSMPay.CMPP3.SMS
{
    /// <summary>
    /// CMPP30 的摘要说明。
    /// </summary>


    public class CMPP30
    {
        #region Constants  //常量
        public const Byte CMPP_VERSION_30 = 0x30;
        public const Byte CMPP_VERSION_21 = 0x20;
        public const UInt32 CMD_ERROR = 0xFFFFFFFF;
        public const UInt32 CMD_CONNECT = 0x00000001;
        public const UInt32 CMD_CONNECT_RESP = 0x80000001;
        public const UInt32 CMD_TERMINATE = 0x00000002; // 终止连接
        public const UInt32 CMD_TERMINATE_RESP = 0x80000002; // 终止连接应答
        public const UInt32 CMD_SUBMIT = 0x00000004; // 提交短信
        public const UInt32 CMD_SUBMIT_RESP = 0x80000004; // 提交短信应答
        public const UInt32 CMD_DELIVER = 0x00000005; // 短信下发
        public const UInt32 CMD_DELIVER_RESP = 0x80000005; // 下发短信应答
        public const UInt32 CMD_QUERY = 0x00000006; // 短信状态查询
        public const UInt32 CMD_QUERY_RESP = 0x80000006; // 短信状态查询应答
        public const UInt32 CMD_CANCEL = 0x00000007; // 删除短信
        public const UInt32 CMD_CANCEL_RESP = 0x80000007; // 删除短信应答
        public const UInt32 CMD_ACTIVE_TEST = 0x00000008; // 激活测试
        public const UInt32 CMD_ACTIVE_TEST_RESP = 0x80000008; // 激活测试应答
        #endregion

        #region Protected Member Variables; //保护类成员变量
        protected string m_strSPID;//SP企业代码;
        protected string m_strPassword;//SP密码;
        protected string m_strAddress;//短信网关地址
        protected int m_iPort;//短信网关端口号;
        protected static UInt32 m_iSeqID = 0;//命令的序号

        protected int m_iSlidingWindowSize = 16;//滑动窗口大小(W)
        protected int m_iActiveTestSpan = 150;//ACTIVETEST的时间间隔(C,以秒为单位),标准为180
        protected DateTime m_dtLastTransferTime;//最近一次网络传输时间
        protected int m_iTimeOut = 60;//响应超时时间(T,以秒为单位)
        protected int m_iSendCount = 3;//最大发送次数(N)
        protected DATA_PACKAGE[] SlidingWindow = null;
        protected TcpClient m_TcpClient = null;
        protected NetworkStream m_NetworkStream = null;
        protected Queue m_MessageQueue = null;//消息队列，用于保存所有待发送数据
        protected int m_iTcpClientTimeout = 5;//TcpClient接收和发送超时（以秒为单位）
        protected int m_iSendSpan = 10;//发送间隔，以毫秒为单位
        #endregion

        #region Worker Thread //工作线程
        protected System.Threading.Thread m_SendThread = null; //发送线程
        protected System.Threading.Thread m_ReceiveThread = null; //接收线程

        protected AutoResetEvent m_eventSendExit = new AutoResetEvent(false);
        protected AutoResetEvent m_eventReceiveExit = new AutoResetEvent(false);
        protected AutoResetEvent m_eventConnect = new AutoResetEvent(false);
        protected AutoResetEvent m_eventDisconnect = new AutoResetEvent(false);
        protected ManualResetEvent m_eventSend = new ManualResetEvent(false);
        protected ManualResetEvent m_eventReceive = new ManualResetEvent(false);

        protected void SendThreadProc() //发送线程
        {
            while (true)
            {
                if (m_eventSendExit.WaitOne(TimeSpan.FromMilliseconds(0), false))
                {
                    Disconnect();
                    break;
                }
                if (m_eventConnect.WaitOne(TimeSpan.FromMilliseconds(0), false))//连接
                {
                    if (Connect())//连接上，开始发送和接收
                    {
                        m_eventSend.Set();
                        m_eventReceive.Set();
                    }
                    else
                    {
                        Close();
                        Thread.Sleep(5000);
                        m_eventConnect.Set();
                    }
                }
                if (m_eventDisconnect.WaitOne(TimeSpan.FromMilliseconds(0), false))//拆除连接
                {
                    m_eventSend.Reset();
                    m_eventReceive.Reset();
                    Disconnect();
                    Thread.Sleep(5000);
                    m_eventConnect.Set();
                }
                if ((m_eventSend.WaitOne(TimeSpan.FromMilliseconds(0), false)) && (m_NetworkStream != null))
                {
                    bool bOK = true;
                    ActiveTest();
                    Monitor.Enter(SlidingWindow);
                    for (int i = 0; i < m_iSlidingWindowSize; i++)//首先用消息队列中的数据填充滑动窗口
                    {
                        if (SlidingWindow[i].Status == 0)
                        {
                            DATA_PACKAGE dp = new DATA_PACKAGE();
                            dp.Data = null;
                            Monitor.Enter(m_MessageQueue);
                            if (m_MessageQueue.Count > 0)
                            {
                                dp = (DATA_PACKAGE)m_MessageQueue.Dequeue();
                                SlidingWindow[i] = dp;
                            }
                            Monitor.Exit(m_MessageQueue);

                        }

                    }
                    for (int i = 0; i < m_iSlidingWindowSize; i++)
                    {
                        DATA_PACKAGE dp = SlidingWindow[i];
                        if ((dp.Status == 1) && (dp.SendCount == 0))//第一次发送
                        {
                            bOK = Send(dp);
                            if ((bOK) && (dp.Command > 0x80000000))//发送的是Response类的消息，不需等待Response
                            {
                                SlidingWindow[i].Status = 0;//清空窗口
                            }
                            else if ((bOK) && (dp.Command < 0x80000000))//发送的是需要等待Response的消息
                            {

                                SlidingWindow[i].SendTime = DateTime.Now;
                                SlidingWindow[i].SendCount++;
                            }
                            else
                            {
                                bOK = false;
                                break;//网络出错
                            }

                        }
                        else if ((dp.Status == 1) && (dp.SendCount > 0))//第N次发送
                        {
                            if (dp.SendCount > m_iSendCount - 1)//已发送m_iSendCount次,丢弃数据包
                            {
                                SlidingWindow[i].Status = 0;//清空窗口
                                if (dp.Command == CMPP30.CMD_ACTIVE_TEST)//是ActiveTest
                                {
                                    bOK = false;
                                    break;//ActiveTest出错
                                }


                            }
                            else
                            {
                                TimeSpan ts = DateTime.Now - dp.SendTime;
                                if (ts.TotalSeconds >= m_iTimeOut)//超时后未收到回应包
                                {
                                    bOK = Send(dp);//再次发送
                                    if (bOK)
                                    {
                                        SlidingWindow[i].SendTime = DateTime.Now;
                                        SlidingWindow[i].SendCount++;
                                    }
                                    else
                                    {
                                        bOK = false;
                                        break;//网络出错
                                    }
                                }
                            }

                        }
                    }
                    Monitor.Exit(SlidingWindow);


                    if (!bOK)
                    {
                        Close();//关闭连接
                        Thread.Sleep(5000);//等待5秒
                        m_eventSend.Reset();
                        m_eventConnect.Set();
                    }
                }
            }

        }
        protected void ReceiveThreadProc() //接收线程
        {
            while (true)
            {
                if (m_eventReceiveExit.WaitOne(TimeSpan.FromMilliseconds(0), false))
                {
                    break;
                }
                if ((m_eventReceive.WaitOne(TimeSpan.FromMilliseconds(0), false) && (m_NetworkStream != null)))
                {
                    CMPP_HEAD Head = ReadHead();
                    if (Head.CommandID == CMPP30.CMD_SUBMIT_RESP)
                    {
                        ReadSubmitResp(Head);
                    }
                    else if (Head.CommandID == CMPP30.CMD_ACTIVE_TEST)
                    {
                        ActiveTestResponse(Head.SequenceID);
                    }
                    else if (Head.CommandID == CMPP30.CMD_ACTIVE_TEST_RESP)
                    {
                        ReadActiveTestResponse(Head);
                    }
                    else if (Head.CommandID == CMPP30.CMD_DELIVER)
                    {
                        ReadDeliver(Head);
                    }
                    else if (Head.CommandID == CMPP30.CMD_ERROR)//网络故障
                    {
                        m_eventReceive.Reset();
                        m_eventDisconnect.Set();
                    }
                }

            }
        }
        #endregion

        #region Constructor //构造器

        public CMPP30(string SPID, string Password, string Address, int Port)
        {
            m_strSPID = SPID;
            m_strPassword = Password;
            m_strAddress = Address;
            m_iPort = Port;
            SlidingWindow = new DATA_PACKAGE[m_iSlidingWindowSize];//初始化滑动窗口
            for (int i = 0; i < m_iSlidingWindowSize; i++)
                SlidingWindow[i] = new DATA_PACKAGE();
            m_MessageQueue = new Queue();

        }
        #endregion

        #region SMSEvents //SMS事件
        public event PRT.GDSMPay.CMPP3.SMS.SMSEventHandler SMSStateChanged;

        protected void RaiseEvent(SMS_STATE State, Object Data)
        {
            if (null != SMSStateChanged)
            {
                SMSEventArgs e = new SMSEventArgs();
                e.Time = DateTime.Now;
                e.State = State;
                e.Data = Data;
                SMSStateChanged(this, e);
            }

        }
        #endregion

        #region Protected Methods //保护方法
        protected UInt32 TimeStamp(DateTime dt)//时间戳
        {
            string str = String.Format("{0:MMddhhmmss}", dt);
            return Convert.ToUInt32(str);
        }
        protected UInt32 CreateID()//创建标识
        {
            UInt32 id = m_iSeqID;
            m_iSeqID++;
            if (m_iSeqID > UInt32.MaxValue)
                m_iSeqID = 0;
            return id;
        }
        protected Byte[] CreateDigest(DateTime dt) //创建摘要
        {
            int iLength = 25 + m_strPassword.Length;
            Byte[] btContent = new Byte[iLength];
            Array.Clear(btContent, 0, iLength);
            int iPos = 0;
            foreach (char ch in m_strSPID)
            {
                btContent[iPos] = (Byte)ch;
                iPos++;
            }
            iPos += 9;
            foreach (char ch in m_strPassword)
            {
                btContent[iPos] = (Byte)ch;
                iPos++;
            }
            string strTimeStamp = String.Format("{0:MMddhhmmss}", dt);
            foreach (char ch in strTimeStamp)
            {
                btContent[iPos] = (Byte)ch;
                iPos++;
            }
            MD5 md5 = new MD5CryptoServiceProvider();
            return md5.ComputeHash(btContent);
        }

        protected bool Close()//关闭
        {
            if (m_NetworkStream != null)
                m_NetworkStream.Close();
            if (m_TcpClient != null)
                m_TcpClient.Close();

            m_TcpClient = null;
            m_NetworkStream = null;

            return true;

        }

        protected bool Connect()//连接
        {
            bool bOK = true;
            string strError = string.Empty;
            CMPP_CONNECT_RESP resp = new CMPP_CONNECT_RESP();
            try
            {
                m_TcpClient = new TcpClient();
                m_TcpClient.ReceiveTimeout = m_TcpClient.SendTimeout = m_iTcpClientTimeout * 1000;
                m_TcpClient.Connect(m_strAddress, m_iPort);
                m_NetworkStream = m_TcpClient.GetStream();

                DateTime dt = DateTime.Now;
                CMPP_CONNECT conn = new CMPP_CONNECT();
                conn.Head = new CMPP_HEAD();
                conn.Head.CommandID = CMPP30.CMD_CONNECT;
                conn.Head.SequenceID = CreateID();
                conn.SourceAddress = m_strSPID;
                conn.TimeStamp = TimeStamp(dt);
                conn.AuthenticatorSource = CreateDigest(dt);
                conn.Version = CMPP_VERSION_30;
                Byte[] buffer = conn.GetBuffer();
                m_NetworkStream.Write(buffer, 0, (Int32)conn.Head.TotalLength);
                int iSpan = 0;
                bool bTimeOut = false;
                while (!m_NetworkStream.DataAvailable)//等待RESPONSE 5秒
                {
                    Thread.Sleep(10);
                    iSpan++;
                    if (iSpan > 500)
                    {
                        bTimeOut = true;
                        break;
                    }

                }
                if (!bTimeOut)
                {
                    CMPP_HEAD Head = ReadHead();
                    if (Head.CommandID == CMD_CONNECT_RESP)
                    {
                        resp = ReadConnectResp(Head);
                        if (resp.Status == 0)
                            bOK = true;
                        else
                        {
                            bOK = false;
                            strError = "未正确接收CONNECT_RESP";
                        }
                    }
                }
                else
                {
                    bOK = false;
                    strError = "等待CONNECT_RESP超时";
                }
            }
            catch (Exception e)
            {
                strError = e.Message;
                bOK = false;
            }

            if (bOK)
                RaiseEvent(SMS_STATE.SP_CONNECT, resp);
            else
                RaiseEvent(SMS_STATE.SP_CONNECT_ERROR, strError);

            return bOK;

        }
        protected bool Disconnect()//断开
        {
            bool bOK = true;
            string strError = string.Empty;
            try
            {
                DateTime dt = DateTime.Now;
                CMPP_HEAD Head = new CMPP_HEAD();
                Head.CommandID = CMPP30.CMD_TERMINATE;
                Head.SequenceID = CreateID();
                Head.TotalLength = (UInt32)Marshal.SizeOf(Head);
                Byte[] buffer = Head.GetBuffer();
                m_NetworkStream.Write(buffer, 0, (Int32)Head.TotalLength);
                int iSpan = 0;
                bool bTimeOut = false;
                while (!m_NetworkStream.DataAvailable)//等待RESPONSE 5秒
                {
                    Thread.Sleep(10);
                    iSpan++;
                    if (iSpan > 500)
                    {
                        bTimeOut = true;
                        break;
                    }

                }
                if (!bTimeOut)
                {
                    Head = ReadHead();
                    if (Head.CommandID == CMD_TERMINATE_RESP)
                        bOK = true;
                    else
                    {
                        bOK = false;
                        strError = "未正确接收TERMINATE_RESP";
                    }
                }
                else
                {
                    bOK = false;
                    strError = "等待TERMINATE_RESP超时";
                }

            }
            catch (Exception ex)
            {
                bOK = false;
                strError = ex.Message;
            }
            if (m_NetworkStream != null)
                m_NetworkStream.Close();
            if (m_TcpClient != null)
                m_TcpClient.Close();
            m_TcpClient = null;
            m_NetworkStream = null;

            if (bOK)
                RaiseEvent(SMS_STATE.SP_DISCONNECT, null);
            else
                RaiseEvent(SMS_STATE.SP_DISCONNECT_ERROR, strError);

            return bOK;
        }
        protected bool Send(DATA_PACKAGE dp)//发送
        {
            bool bOK = true;
            string strError = string.Empty;
            SMS_STATE state = SMS_STATE.UNKNOW_ERROR;
            try
            {
                Thread.Sleep(m_iSendSpan);
                Byte[] btData = null;
                if (dp.Command == CMD_ACTIVE_TEST)
                {
                    btData = ((CMPP_HEAD)dp.Data).GetBuffer();
                    state = SMS_STATE.ACTIVE_TEST;
                }
                else if (dp.Command == CMD_ACTIVE_TEST_RESP)
                {
                    btData = ((CMPP_ACTIVE_TEST_RESP)dp.Data).GetBuffer();
                    state = SMS_STATE.ACTIVE_TEST_RESPONSE;
                }
                else if (dp.Command == CMD_DELIVER_RESP)
                {
                    btData = ((CMPP_DELIVER_RESP)dp.Data).GetBuffer();
                    state = SMS_STATE.DELIVER_RESPONSE;
                }
                else if (dp.Command == CMD_SUBMIT)
                {
                    btData = ((CMPP_SUBMIT)dp.Data).GetBuffer();
                    state = SMS_STATE.SUBMIT;
                }
                m_NetworkStream.Write(btData, 0, btData.Length);
                m_dtLastTransferTime = DateTime.Now;
            }
            catch (Exception ex)
            {

                bOK = false;
                strError = ex.Message;
            }
            if (bOK)
            {
                RaiseEvent(state, dp.Data);
            }
            else
            {
                if (state == SMS_STATE.ACTIVE_TEST)
                    state = SMS_STATE.ACTIVE_TEST_ERROR;
                else if (state == SMS_STATE.ACTIVE_TEST_RESPONSE)
                    state = SMS_STATE.ACTIVE_TEST_RESPONSE_ERROR;
                else if (state == SMS_STATE.DELIVER_RESPONSE)
                    state = SMS_STATE.DELIVER_RESPONSE_ERROR;
                else if (state == SMS_STATE.SUBMIT)
                    state = SMS_STATE.SUBMIT_ERROR;

                RaiseEvent(state, strError);
            }
            return bOK;

        }
        protected CMPP_HEAD ReadHead()//读头
        {
            CMPP_HEAD head = new CMPP_HEAD();
            head.CommandID = 0;
            Byte[] buffer = new Byte[12];
            try
            {
                if (m_NetworkStream.DataAvailable)
                {
                    m_NetworkStream.Read(buffer, 0, buffer.Length);
                    head.TotalLength = (UInt32)Utility.NetBytesToInt(buffer, 0, 4);
                    head.CommandID = (UInt32)Utility.NetBytesToInt(buffer, 4, 4);
                    head.SequenceID = (UInt32)Utility.NetBytesToInt(buffer, 8, 4);
                }

            }
            catch
            {
                head.CommandID = CMD_ERROR;
            }
            return head;
        }

        protected CMPP_CONNECT_RESP ReadConnectResp(CMPP_HEAD Head)//读连接响应
        {
            CMPP_CONNECT_RESP resp = new CMPP_CONNECT_RESP();
            resp.Head = Head;
            try
            {
                if (m_NetworkStream.DataAvailable)
                {
                    Byte[] buffer = new Byte[resp.Head.TotalLength - Marshal.SizeOf(resp.Head)];
                    m_NetworkStream.Read(buffer, 0, buffer.Length);
                    resp.Status = (UInt32)Utility.NetBytesToInt(buffer, 0, 4);
                    resp.AuthenticatorISMG = new Byte[16];
                    Array.Copy(buffer, 4, resp.AuthenticatorISMG, 0, 16);
                    resp.Version = buffer[buffer.Length - 1];
                }
            }
            catch
            {
                resp.Head.CommandID = CMD_ERROR;
            }

            return resp;


        }
        protected CMPP_SUBMIT_RESP ReadSubmitResp(CMPP_HEAD Head)//读提交响应
        {
            CMPP_SUBMIT_RESP resp = new CMPP_SUBMIT_RESP();
            resp.Head = Head;
            string strError = string.Empty;
            bool bOK = true;
            try
            {
                if (m_NetworkStream.DataAvailable)
                {
                    Byte[] buffer = new Byte[resp.Head.TotalLength - Marshal.SizeOf(resp.Head)];
                    m_NetworkStream.Read(buffer, 0, buffer.Length);
                    //resp.MsgID=(UInt64)Utility.NetBytesToInt(buffer,0,8);
                    resp.Msg_ID = (UInt64)BitConverter.ToUInt64(buffer, 0);
                    resp.Result = (UInt32)Utility.NetBytesToInt(buffer, 8, 4);
                    Monitor.Enter(SlidingWindow);
                    for (int i = 0; i < m_iSlidingWindowSize; i++)
                    {
                        if ((SlidingWindow[i].Status == 1) &&//已发送，等待回应
                         (SlidingWindow[i].SequenceID == resp.Head.SequenceID) &&//序列号相同
                         (SlidingWindow[i].Command == CMD_SUBMIT))//是Submit
                        {
                            SlidingWindow[i].Status = 0;//清空窗口
                            break;
                        }
                    }
                    Monitor.Exit(SlidingWindow);
                }

            }
            catch (Exception ex)
            {
                resp.Head.CommandID = CMD_ERROR;
                strError = ex.Message;
                bOK = false;
            }
            if (bOK)
                RaiseEvent(SMS_STATE.SUBMIT_RESPONSE, resp);
            else
                RaiseEvent(SMS_STATE.SUBMIT_RESPONSE_ERROR, strError);
            return resp;

        }

        protected CMPP_DELIVER ReadDeliver(CMPP_HEAD Head)//读下发
        {
            CMPP_DELIVER deliver = new CMPP_DELIVER();
            deliver.Head = Head;
            string strError = string.Empty;
            try
            {
                if (m_NetworkStream.DataAvailable)
                {
                    Byte[] buffer = new Byte[deliver.Head.TotalLength - Marshal.SizeOf(deliver.Head)];
                    m_NetworkStream.Read(buffer, 0, buffer.Length);
                    deliver.Init(buffer);
                    DeliverResponse(deliver.Head.SequenceID, deliver.Msg_ID, 0);
                }
            }
            catch (Exception ex)
            {
                deliver.Head.CommandID = CMD_ERROR;
                strError = ex.Message;
            }
            if ((deliver.Head.CommandID == CMD_DELIVER) && (deliver.Registered_Delivery == 0))////是短消息
            {
                RaiseEvent(SMS_STATE.DELIVER, deliver);
            }
            else if ((deliver.Head.CommandID == CMD_DELIVER) && (deliver.Registered_Delivery == 1))////是状态报告
            {
                RaiseEvent(SMS_STATE.REPORT, deliver);
            }
            else//错误
            {
                RaiseEvent(SMS_STATE.DELIVER_ERROR, strError);
            }
            return deliver;

        }

        protected bool DeliverResponse(UInt32 SequenceID, UInt64 Msg_Id, UInt32 Result)//释放响应
        {
            bool bOK = true;
            string strError = string.Empty;
            CMPP_DELIVER_RESP resp = new CMPP_DELIVER_RESP();
            resp.Head = new CMPP_HEAD();
            resp.Head.CommandID = CMPP30.CMD_DELIVER_RESP;
            resp.Head.SequenceID = SequenceID;
            resp.Msg_Id = Msg_Id;
            resp.Result = Result;

            DATA_PACKAGE dp = new DATA_PACKAGE();
            dp.SequenceID = resp.Head.SequenceID;
            dp.Command = resp.Head.CommandID;
            dp.SendCount = 0;
            dp.Data = resp;
            dp.Status = 1;

            Monitor.Enter(m_MessageQueue);
            m_MessageQueue.Enqueue(dp);
            Monitor.Exit(m_MessageQueue);

            return bOK;
        }

        protected bool ActiveTest()//激活测试
        {
            bool bOK = true;
            TimeSpan ts = DateTime.Now - m_dtLastTransferTime;
            if (ts.TotalSeconds > m_iActiveTestSpan)
            {
                CMPP_HEAD Head = new CMPP_HEAD();
                Head.CommandID = CMPP30.CMD_ACTIVE_TEST;
                Head.SequenceID = CreateID();
                Head.TotalLength = 12;

                DATA_PACKAGE dp = new DATA_PACKAGE();
                dp.SequenceID = Head.SequenceID;
                dp.Command = Head.CommandID;
                dp.SendCount = 0;
                dp.Data = Head;
                dp.Status = 1;

                Monitor.Enter(m_MessageQueue);
                m_MessageQueue.Enqueue(dp);
                Monitor.Exit(m_MessageQueue);

            }
            return bOK;

        }
        protected CMPP_ACTIVE_TEST_RESP ReadActiveTestResponse(CMPP_HEAD Head)//读激活测试响应
        {
            CMPP_ACTIVE_TEST_RESP resp = new CMPP_ACTIVE_TEST_RESP();
            resp.Head = Head;
            string strError = string.Empty;
            bool bOK = true;
            try
            {
                if (m_NetworkStream.DataAvailable)
                {
                    Byte[] buffer = new Byte[resp.Head.TotalLength - Marshal.SizeOf(resp.Head)];
                    m_NetworkStream.Read(buffer, 0, buffer.Length);
                    resp.Reserved = buffer[0];
                    Monitor.Enter(SlidingWindow);
                    for (int i = 0; i < m_iSlidingWindowSize; i++)
                    {
                        if ((SlidingWindow[i].Status == 1) &&//已发送，等待回应
                         (SlidingWindow[i].SequenceID == resp.Head.SequenceID) &&//序列号相同
                         (SlidingWindow[i].Command == CMD_ACTIVE_TEST))//是ACTIVE_TEST
                        {
                            SlidingWindow[i].Status = 0;//清空窗口
                            break;
                        }
                    }
                    Monitor.Exit(SlidingWindow);
                }

            }
            catch (Exception ex)
            {
                resp.Head.CommandID = CMD_ERROR;
                strError = ex.Message;
                bOK = false;
            }

            if (bOK)
                RaiseEvent(SMS_STATE.ACTIVE_TEST_RESPONSE, resp);
            else
                RaiseEvent(SMS_STATE.ACTIVE_TEST_RESPONSE_ERROR, strError);

            return resp;
        }
        protected bool ActiveTestResponse(UInt32 SequenceID)//激活测试响应
        {
            bool bOK = true;
            CMPP_ACTIVE_TEST_RESP resp = new CMPP_ACTIVE_TEST_RESP();
            resp.Head = new CMPP_HEAD();
            resp.Head.CommandID = CMPP30.CMD_ACTIVE_TEST_RESP;
            resp.Head.SequenceID = SequenceID;
            resp.Reserved = 0;

            DATA_PACKAGE dp = new DATA_PACKAGE();
            dp.SequenceID = resp.Head.SequenceID;
            dp.Command = resp.Head.CommandID;
            dp.SendCount = 0;
            dp.Data = resp;
            dp.Status = 1;

            Monitor.Enter(m_MessageQueue);
            m_MessageQueue.Enqueue(dp);
            Monitor.Exit(m_MessageQueue);

            return bOK;
        }


        #endregion

        #region Public Methods //公共方法
        //开始线程
        public void StartThread()//开始线程
        {
            if (m_SendThread == null)
            {
                m_dtLastTransferTime = DateTime.Now;
                m_SendThread = new Thread(new ThreadStart(this.SendThreadProc));
                //m_SendThread.IsBackground=false;
                m_SendThread.Name = m_strSPID + "_Send";
                m_SendThread.Start();
            }
            if (m_ReceiveThread == null)
            {
                m_ReceiveThread = new Thread(new ThreadStart(this.ReceiveThreadProc));
                //m_ReceiveThread.IsBackground=false;
                m_ReceiveThread.Name = m_strSPID + "_Receive";
                m_ReceiveThread.Start();
            }
            m_eventConnect.Set();

        }
        //结束线程
        public void EndThread()//结束线程
        {
            m_eventSend.Reset();
            m_eventReceive.Reset();
            m_eventReceiveExit.Set();
            m_eventSendExit.Set();
        }
        //发送函数
        public bool Submit(string Message,
         string[] Destination,
         string Source,
         string ServiceID,
         CODING Coding,
         bool NeedReport,
         byte FeeUserType,
         byte FeeType,
         int InfoFee,
         string FeeUser,
         string LinkID)//发送提交
        {
            bool bOK = true;
            string strError = string.Empty;
            CMPP_SUBMIT submit = new CMPP_SUBMIT();
            submit.Head = new CMPP_HEAD();
            submit.Head.CommandID = CMPP30.CMD_SUBMIT;
            submit.Head.SequenceID = CreateID();
            submit.Msg_ID = 0;
            submit.Pk_Total = 1;
            submit.Pk_Number = 1;
            submit.Registered_Delivery = Convert.ToByte(NeedReport);
            submit.Msg_Level = 0;
            submit.Service_Id = ServiceID;
            submit.Fee_UserType = FeeUserType;
            submit.Fee_Terminal_Id = FeeUser;
            submit.Fee_Terminal_Type = 0;//真实号码
            submit.TP_Pid = 0;
            submit.TP_Udhi = 0;
            submit.Msg_Fmt = (Byte)Coding;
            submit.Msg_Src = m_strSPID;
            submit.FeeType = string.Format("{0:d2}", FeeType);
            submit.FeeCode = InfoFee.ToString();
            submit.Valid_Time = string.Empty;
            submit.At_Time = string.Empty;
            submit.Src_Id = Source;
            submit.DestUsr_Tl = (Byte)Destination.Length;
            submit.Dest_Terminal_ID = Destination;
            submit.Dest_Terminal_Type = 0;//真实号码
            submit.Msg_Length = (Byte)Utility.CountLength(Message, Coding);
            submit.Msg_Content = Message;
            submit.LinkID = LinkID;


            DATA_PACKAGE dp = new DATA_PACKAGE();
            dp.SequenceID = submit.Head.SequenceID;
            dp.Command = submit.Head.CommandID;
            dp.SendCount = 0;
            dp.Data = submit;
            dp.Status = 1;

            Monitor.Enter(m_MessageQueue);
            m_MessageQueue.Enqueue(dp);
            Monitor.Exit(m_MessageQueue);

            return bOK;
        }
        #endregion
    }
    //异步发送回调函数
    public delegate bool CMPPAsyncSubmit(string Message,
             string[] Destination,
             string Source,
             string ServiceID,
             CODING Coding,
             bool NeedReport,
             byte FeeUserType,
             byte FeeType,
             int InfoFee,
             string FeeUser,
             string LinkID);

}

