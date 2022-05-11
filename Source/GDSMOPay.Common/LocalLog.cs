using System;
using System.Collections.Generic;
using System.Text;
namespace PRT.GDSMPay.Common
{
    public static class LocalLog
    {
        static string sPath = System.Windows.Forms.Application.StartupPath + "\\cfg.ini"; //配置文件路径
        //声明INI文件的写操作函数 WritePrivateProfileString()
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        //声明INI文件的读操作函数 GetPrivateProfileString()
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public static void WriteValue(string section, string key, string value)
        {
            /* 写入值到ini文件 */
            // section=配置节，key=键名，value=键值，path=路径
            WritePrivateProfileString(section, key, value, sPath);
        }

        public static string ReadValue(string section, string key)
        {
            /* 从ini文件读取值 */
            // 每次从ini中读取多少字节
            System.Text.StringBuilder temp = new System.Text.StringBuilder(255);

            // section=配置节，key=键名，temp=上面，path=路径
            GetPrivateProfileString(section, key, "", temp, 255, sPath);
            return temp.ToString();
        }

        public static void WriteLogFile(Exception ex)
        {
            string sErrStr = "\r\n";
            sErrStr += "错误消息:" + "\r\n";
            sErrStr += "错误源:" + ex.Source + "\r\n";
            sErrStr += "调用堆栈:" + ex.StackTrace + "\r\n";
            sErrStr += "错误位置:" + ex.TargetSite + "\r\n";
            sErrStr += "帮助链接:" + ex.HelpLink + "\r\n";
            sErrStr += "错误时间:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "\r\n";
            /////如果系统出现异常则将错误信息写入日志Exception.log
            /**/
            ///指定日志文件的目录
            //string fname = System.Windows.Forms.Application.StartupPath + "\\Exception.log";
            string fname = "c:\\GDSMPayError.log";             
            /**/
            ///定义文件信息对象
            System.IO.FileInfo finfo = new System.IO.FileInfo(fname);
            /**/
            ///判断文件是否存在以及是否大于2M
            if (finfo.Exists && finfo.Length > 2048000)
            {
                finfo.Delete();
            }
            /**/
            ///创建只写文件流
            using (System.IO.FileStream fs = finfo.OpenWrite())
            {
                /**/
                ///根据上面创建的文件流创建写数据流
                System.IO.StreamWriter w = new System.IO.StreamWriter(fs);
                //w.Encoding = Encoding.UTF8;
                /**/
                ///设置写数据流的起始位置为文件流的末尾
                w.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                w.WriteLine(sErrStr, Encoding.UTF8);
                w.WriteLine("----------------------------------------------------------------------------------------------------------------------------------------------------------");
                //清空缓冲区内容，并把缓冲区内容写入基础流
                w.Flush();
                /**/
                ///关闭写数据流
                w.Close();
            }
        }

        public static void WriteMissMessage(string sInfo)
        {
            string fname = "c:\\GDSMPayMissMessage.log";
            /**/
            ///定义文件信息对象
            System.IO.FileInfo finfo = new System.IO.FileInfo(fname);
            /**/
            ///判断文件是否存在以及是否大于2M
            if (finfo.Exists && finfo.Length > 2048000)
            {
                finfo.Delete();
            }
            /**/
            ///创建只写文件流
            using (System.IO.FileStream fs = finfo.OpenWrite())
            {
                /**/
                ///根据上面创建的文件流创建写数据流
                System.IO.StreamWriter w = new System.IO.StreamWriter(fs);
                //w.Encoding = Encoding.UTF8;
                /**/
                ///设置写数据流的起始位置为文件流的末尾
                w.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                w.WriteLine(sInfo, Encoding.UTF8);
                //清空缓冲区内容，并把缓冲区内容写入基础流
                w.Flush();
                /**/
                ///关闭写数据流
                w.Close();
            }
        }

        /****************************/
    }
}
