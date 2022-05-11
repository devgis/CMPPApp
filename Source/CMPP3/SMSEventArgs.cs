using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections;
using System.Diagnostics;
namespace PRT.GDSMPay.CMPP3.SMS
{
    //事件参数定义
    public class SMSEventArgs : EventArgs
    {
        SMS_STATE m_State;
        Object m_Data;
        DateTime m_dtTime;

        public SMS_STATE State
        {
            get { return m_State; }
            set { m_State = value; }
        }

        public object Data
        {
            get { return m_Data; }
            set { m_Data = value; }
        }
        public DateTime Time
        {
            get { return m_dtTime; }
            set { m_dtTime = value; }
        }

    }
    //事件处理函数
    public delegate void SMSEventHandler(object sender, SMSEventArgs e);

    //异步事件回调函数
    public delegate void SMSAsyncEvent(SMSEventArgs e);

    public class Utility
    {
        public static String Decode(Byte[] buf, int StartIndex, int Length, CODING Coding)
        {
            String str = String.Empty;
            if (Coding == CODING.ASCII)
                str = System.Text.Encoding.ASCII.GetString(buf, StartIndex, Length);

            else if (Coding == CODING.UCS2)
                str = System.Text.Encoding.BigEndianUnicode.GetString(buf, 0, Length);
            else if (Coding == CODING.GBK)
                str = System.Text.UnicodeEncoding.GetEncoding("gb2312").GetString(buf, StartIndex, Length);
            return str;
        }
        public static Byte[] Encode(String str, CODING coding)
        {
            Byte[] buf = null;
            if (str == null)
                return buf;
            if (coding == CODING.ASCII)
                buf = System.Text.Encoding.ASCII.GetBytes(str);
            else if (coding == CODING.UCS2)
                buf = System.Text.Encoding.BigEndianUnicode.GetBytes(str);
            else if (coding == CODING.GBK)
                buf = System.Text.UnicodeEncoding.GetEncoding("gb2312").GetBytes(str);

            return buf;

        }
        public static UInt32 CountLength(String str, CODING coding)
        {
            Byte[] buf = Encode(str, coding);
            if (buf != null)
                return (UInt32)buf.Length;
            else
                return 0;
        }

        public static Byte[] IntToNetBytes(object obj)
        {
            Byte[] bytes = null;
            if (obj.GetType() == System.Type.GetType("System.UInt32"))
            {
                UInt32 val = (UInt32)obj;
                bytes = BitConverter.GetBytes(val);
            }
            if (obj.GetType() == System.Type.GetType("System.UInt64"))
            {
                UInt64 val = (UInt64)obj;
                bytes = BitConverter.GetBytes(val);
            }

            if (bytes != null)
                System.Array.Reverse(bytes);

            return bytes;

        }
        public static object NetBytesToInt(Byte[] bytes, int index, int length)
        {
            Array.Reverse(bytes, index, length);
            if (length == 4)
                return BitConverter.ToUInt32(bytes, index);
            else if (length == 8)
                return BitConverter.ToUInt64(bytes, index);
            else
                return 0;
        }
    }

}