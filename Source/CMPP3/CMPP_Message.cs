/*关于联合
CMPP定义的各种数据包采用C语言中的联合（Union)来处理是最有效率的，因为接收和发送的是一个字节数组，使用联合可以方便地在字节数组和数据包结构之间转换。C#本身并不支持联合，但可以使用P/Invoke（平台封送调用）机制实现类似于联合的结构。
在定义CMPP数据包时，我为每个数据包结构定义了Init函数（用于将字节数组转换为数据包结构）和GetBuffer函数（用于将数据包结构转换为字节数组），主要是因为当时对P/Invoke机制不太熟悉。
使用Init和GetBuffer函数的结果是效率要比使用联合低很多，不过压力测试的时候系统表现还是相当稳定的。有兴趣的朋友可以试试用联合实现CMPP的数据包。*/

using System;
using System.Runtime.InteropServices;

namespace PRT.GDSMPay.CMPP3.SMS
{
    public struct DATA_PACKAGE
    {
        public UInt32 Command;
        public UInt32 SequenceID;//流水号
        public object Data;//数据
        public DateTime SendTime; //数据包发送时间
        public int SendCount;//发送次数
        public int Status;//数据包状态 0--空，1--待发送，2--已发送
    }

    #region Enums
    public enum SMS_STATE
    {
        SP_CONNECT, ACTIVE_TEST, ACTIVE_TEST_RESPONSE, SUBMIT, SUBMIT_RESPONSE, DELIVER, DELIVER_RESPONSE, REPORT, SP_DISCONNECT,
        SP_CONNECT_ERROR, ACTIVE_TEST_ERROR, ACTIVE_TEST_RESPONSE_ERROR, SUBMIT_ERROR, SUBMIT_RESPONSE_ERROR, DELIVER_ERROR, DELIVER_RESPONSE_ERROR, SP_DISCONNECT_ERROR, UNKNOW_ERROR

    }

    public enum CODING
    {
        ASCII = 0, BINARY = 4, UCS2 = 8, GBK = 15
    }
    #endregion

    #region CMPP30 Data Packages
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_HEAD
    {
        public UInt32 TotalLength;
        public UInt32 CommandID;
        public UInt32 SequenceID;
        public Byte[] GetBuffer()
        {
            Byte[] buffer = new Byte[Marshal.SizeOf(this)];//12;
            Byte[] temp = null;
            temp = BitConverter.GetBytes(TotalLength);
            buffer[3] = temp[0];
            buffer[2] = temp[1];
            buffer[1] = temp[2];
            buffer[0] = temp[3];
            temp = BitConverter.GetBytes(CommandID);
            buffer[7] = temp[0];
            buffer[6] = temp[1];
            buffer[5] = temp[2];
            buffer[4] = temp[3];
            temp = BitConverter.GetBytes(SequenceID);
            buffer[11] = temp[0];
            buffer[10] = temp[1];
            buffer[9] = temp[2];
            buffer[8] = temp[3];
            return buffer;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_CONNECT
    {
        public CMPP_HEAD Head;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
        public string SourceAddress;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public Byte[] AuthenticatorSource;
        public Byte Version;
        public UInt32 TimeStamp;

        public Byte[] GetBuffer()
        {
            Byte[] temp = null;
            int iPos = 0;
            Head.TotalLength = 39;
            Byte[] buffer = new Byte[39];

            Byte[] HeadBuffer = this.Head.GetBuffer();
            Array.Copy(HeadBuffer, 0, buffer, 0, HeadBuffer.Length);
            iPos = iPos + HeadBuffer.Length;

            temp = Utility.Encode(SourceAddress, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 6;

            Array.Copy(AuthenticatorSource, 0, buffer, iPos, AuthenticatorSource.Length);
            iPos = iPos + AuthenticatorSource.Length;

            buffer[iPos] = Version;
            iPos++;

            //temp=BitConverter.GetBytes(TimeStamp);
            temp = Utility.IntToNetBytes(TimeStamp);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + temp.Length;

            return buffer;
        }

    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_CONNECT_RESP
    {
        public CMPP_HEAD Head;
        public UInt32 Status;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public Byte[] AuthenticatorISMG;
        public Byte Version;

    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_SUBMIT
    {
        public CMPP_HEAD Head;
        public UInt64 Msg_ID;
        public Byte Pk_Total;
        public Byte Pk_Number;
        public Byte Registered_Delivery;
        public Byte Msg_Level;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string Service_Id;
        public Byte Fee_UserType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Fee_Terminal_Id;
        public Byte Fee_Terminal_Type;
        public Byte TP_Pid;
        public Byte TP_Udhi;
        public Byte Msg_Fmt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
        public string Msg_Src;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2)]
        public string FeeType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
        public string FeeCode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
        public string Valid_Time;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
        public string At_Time;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
        public string Src_Id;
        public Byte DestUsr_Tl;
        public string[] Dest_Terminal_ID;
        public Byte Dest_Terminal_Type;
        public Byte Msg_Length;
        public string Msg_Content;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public String LinkID;

        public Byte[] GetBuffer()
        {

            int iPos = 0;
            Msg_Length = (Byte)Utility.CountLength(Msg_Content.ToString(), (CODING)Msg_Fmt);
            Head.TotalLength = (UInt32)(163 + 32 * DestUsr_Tl + Msg_Length);
            Byte[] buffer = new Byte[Head.TotalLength];
            Byte[] temp = null;

            Byte[] HeadBuffer = this.Head.GetBuffer();
            Array.Copy(HeadBuffer, 0, buffer, 0, HeadBuffer.Length);
            iPos = HeadBuffer.Length;

            temp = Utility.IntToNetBytes(Msg_ID);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + temp.Length;

            buffer[iPos] = Pk_Total;
            iPos++;

            buffer[iPos] = Pk_Number;
            iPos++;

            buffer[iPos] = Registered_Delivery;
            iPos++;

            buffer[iPos] = Msg_Level;
            iPos++;

            temp = Utility.Encode(Service_Id, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 10;

            buffer[iPos] = Fee_UserType;
            iPos++;

            temp = Utility.Encode(Fee_Terminal_Id, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 32;

            buffer[iPos] = Fee_Terminal_Type;
            iPos++;

            buffer[iPos] = TP_Pid;
            iPos++;

            buffer[iPos] = TP_Udhi;
            iPos++;

            buffer[iPos] = Msg_Fmt;
            iPos++;

            temp = Utility.Encode(Msg_Src, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 6;

            temp = Utility.Encode(FeeType, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 2;

            temp = Utility.Encode(FeeCode, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 6;

            temp = Utility.Encode(Valid_Time, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 17;

            temp = Utility.Encode(At_Time, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 17;

            temp = Utility.Encode(Src_Id, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + 21;

            buffer[iPos] = DestUsr_Tl;
            iPos++;

            for (int i = 0; i < DestUsr_Tl; i++)
            {
                temp = Utility.Encode(Dest_Terminal_ID[i], CODING.ASCII);
                Array.Copy(temp, 0, buffer, iPos, temp.Length);
                iPos = iPos + 32;
            }

            buffer[iPos] = Dest_Terminal_Type;
            iPos++;

            buffer[iPos] = Msg_Length;
            iPos++;

            temp = Utility.Encode(Msg_Content, (CODING)Msg_Fmt);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + temp.Length;

            temp = Utility.Encode(LinkID, CODING.ASCII);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + temp.Length;

            return buffer;

        }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_SUBMIT_RESP
    {
        public CMPP_HEAD Head;
        public UInt64 Msg_ID;
        public UInt32 Result;

    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_DELIVER
    {
        public CMPP_HEAD Head;
        public UInt64 Msg_ID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
        public string Dest_Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string Service_Id;
        public Byte TP_Pid;
        public Byte TP_Udhi;
        public Byte Msg_Fmt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string Src_Terminal_Id;
        public Byte Src_Terminal_Type;
        public Byte Registered_Delivery;
        public Byte Msg_Length;
        public string Msg_Content;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public String LinkID;

        public bool Init(Byte[] buffer)
        {
            int iPos = 0;
            bool bOK = true;
            try
            {
                Msg_ID = (UInt64)BitConverter.ToUInt64(buffer, 0);
                iPos = iPos + 8;

                Dest_Id = Utility.Decode(buffer, iPos, 21, CODING.ASCII);
                iPos = iPos + 21;

                Service_Id = Utility.Decode(buffer, iPos, 10, CODING.ASCII);
                iPos = iPos + 10;

                TP_Pid = buffer[iPos];
                iPos++;

                TP_Udhi = buffer[iPos];
                iPos++;

                Msg_Fmt = buffer[iPos];
                iPos++;

                Src_Terminal_Id = Utility.Decode(buffer, iPos, 32, CODING.ASCII);
                iPos = iPos + 32;

                Src_Terminal_Type = buffer[iPos];
                iPos++;

                Registered_Delivery = buffer[iPos];
                iPos++;

                Msg_Length = buffer[iPos];
                iPos++;

                if (Registered_Delivery == 0)//是短消息
                {
                    Msg_Content = Utility.Decode(buffer, iPos, Msg_Length, (CODING)Msg_Fmt);

                }
                else//是状态报告,先转为BASE64 String 存储
                    Msg_Content = Convert.ToBase64String(buffer, iPos, Msg_Length);

                iPos = iPos + Msg_Length;
                LinkID = Utility.Decode(buffer, iPos, 20, CODING.ASCII);
            }
            catch
            {
                bOK = false;
            }
            return bOK;

        }
        public CMPP_REPORT GetReport()
        {
            CMPP_REPORT Report = new CMPP_REPORT();
            if (Registered_Delivery == 1)//是状态报告
            {
                Byte[] bytes = Convert.FromBase64String(Msg_Content);
                if ((bytes != null) && (bytes.Length > 0))
                    Report.Init(bytes);
            }
            return Report;

        }

    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_REPORT
    {
        public UInt64 Msg_Id;
        public string Stat;
        public string Submit_Time;
        public string Done_Time;
        public string Dest_Terminal_Id;
        public UInt32 SMSC_Sequence;

        public bool Init(Byte[] buffer)
        {
            int iPos = 0;
            bool bOK = true;
            try
            {
                Msg_Id = (UInt64)Utility.NetBytesToInt(buffer, 0, 8);
                iPos += 8;

                Stat = Utility.Decode(buffer, iPos, 7, CODING.ASCII);
                iPos += 7;

                Submit_Time = Utility.Decode(buffer, iPos, 10, CODING.ASCII);
                iPos += 10;

                Done_Time = Utility.Decode(buffer, iPos, 10, CODING.ASCII);
                iPos += 10;

                Dest_Terminal_Id = Utility.Decode(buffer, iPos, 32, CODING.ASCII);
                iPos += 32;

                SMSC_Sequence = (UInt32)Utility.NetBytesToInt(buffer, iPos, 4);
            }
            catch
            {
                bOK = false;
            }
            return bOK;

        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_DELIVER_RESP
    {
        public CMPP_HEAD Head;
        public UInt64 Msg_Id;
        public UInt32 Result;
        public Byte[] GetBuffer()
        {
            int iPos = 0;
            Head.TotalLength = (UInt32)Marshal.SizeOf(this);
            Byte[] buffer = new Byte[Head.TotalLength];
            Byte[] temp = null;

            Byte[] HeadBuffer = this.Head.GetBuffer();
            Array.Copy(HeadBuffer, 0, buffer, 0, HeadBuffer.Length);
            iPos = HeadBuffer.Length;

            temp = BitConverter.GetBytes(Msg_Id);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + temp.Length;

            temp = Utility.IntToNetBytes(Result);
            Array.Copy(temp, 0, buffer, iPos, temp.Length);
            iPos = iPos + temp.Length;

            return buffer;

        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_ACTIVE_TEST_RESP
    {
        public CMPP_HEAD Head;
        public Byte Reserved;
        public Byte[] GetBuffer()
        {
            int iPos = 0;
            Head.TotalLength = (UInt32)Marshal.SizeOf(this);
            Byte[] buffer = new Byte[Head.TotalLength];

            Byte[] HeadBuffer = this.Head.GetBuffer();
            Array.Copy(HeadBuffer, 0, buffer, 0, HeadBuffer.Length);
            iPos = HeadBuffer.Length;

            buffer[iPos] = Reserved;

            return buffer;

        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_CANCEL
    {
        public CMPP_HEAD Head;
        public UInt64 MsgID;
        public Byte[] GetBuffer()
        {
            Byte[] buffer = new Byte[Marshal.SizeOf(this)];
            Byte[] HeadBuffer = this.Head.GetBuffer();
            int iPos = HeadBuffer.Length;
            Array.Copy(HeadBuffer, 0, buffer, 0, iPos);
            Byte[] temp = BitConverter.GetBytes(MsgID);
            buffer[iPos + 7] = temp[0];
            buffer[iPos + 6] = temp[1];
            buffer[iPos + 5] = temp[2];
            buffer[iPos + 4] = temp[3];
            buffer[iPos + 3] = temp[4];
            buffer[iPos + 2] = temp[5];
            buffer[iPos + 1] = temp[6];
            buffer[iPos + 0] = temp[7];

            return buffer;
        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CMPP_CANCEL_RESP
    {
        public CMPP_HEAD Head;
        public UInt32 SuccessID;
    }

    #endregion

}
