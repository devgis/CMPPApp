using System;
using System.Collections.Generic;
using System.Text;
namespace PRT.GDSMPay.Common
{
    public static class LocalLog
    {
        static string sPath = System.Windows.Forms.Application.StartupPath + "\\cfg.ini"; //�����ļ�·��
        //����INI�ļ���д�������� WritePrivateProfileString()
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        //����INI�ļ��Ķ��������� GetPrivateProfileString()
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public static void WriteValue(string section, string key, string value)
        {
            /* д��ֵ��ini�ļ� */
            // section=���ýڣ�key=������value=��ֵ��path=·��
            WritePrivateProfileString(section, key, value, sPath);
        }

        public static string ReadValue(string section, string key)
        {
            /* ��ini�ļ���ȡֵ */
            // ÿ�δ�ini�ж�ȡ�����ֽ�
            System.Text.StringBuilder temp = new System.Text.StringBuilder(255);

            // section=���ýڣ�key=������temp=���棬path=·��
            GetPrivateProfileString(section, key, "", temp, 255, sPath);
            return temp.ToString();
        }

        public static void WriteLogFile(Exception ex)
        {
            string sErrStr = "\r\n";
            sErrStr += "������Ϣ:" + "\r\n";
            sErrStr += "����Դ:" + ex.Source + "\r\n";
            sErrStr += "���ö�ջ:" + ex.StackTrace + "\r\n";
            sErrStr += "����λ��:" + ex.TargetSite + "\r\n";
            sErrStr += "��������:" + ex.HelpLink + "\r\n";
            sErrStr += "����ʱ��:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "\r\n";
            /////���ϵͳ�����쳣�򽫴�����Ϣд����־Exception.log
            /**/
            ///ָ����־�ļ���Ŀ¼
            //string fname = System.Windows.Forms.Application.StartupPath + "\\Exception.log";
            string fname = "c:\\GDSMPayError.log";             
            /**/
            ///�����ļ���Ϣ����
            System.IO.FileInfo finfo = new System.IO.FileInfo(fname);
            /**/
            ///�ж��ļ��Ƿ�����Լ��Ƿ����2M
            if (finfo.Exists && finfo.Length > 2048000)
            {
                finfo.Delete();
            }
            /**/
            ///����ֻд�ļ���
            using (System.IO.FileStream fs = finfo.OpenWrite())
            {
                /**/
                ///�������洴�����ļ�������д������
                System.IO.StreamWriter w = new System.IO.StreamWriter(fs);
                //w.Encoding = Encoding.UTF8;
                /**/
                ///����д����������ʼλ��Ϊ�ļ�����ĩβ
                w.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                w.WriteLine(sErrStr, Encoding.UTF8);
                w.WriteLine("----------------------------------------------------------------------------------------------------------------------------------------------------------");
                //��ջ��������ݣ����ѻ���������д�������
                w.Flush();
                /**/
                ///�ر�д������
                w.Close();
            }
        }

        public static void WriteMissMessage(string sInfo)
        {
            string fname = "c:\\GDSMPayMissMessage.log";
            /**/
            ///�����ļ���Ϣ����
            System.IO.FileInfo finfo = new System.IO.FileInfo(fname);
            /**/
            ///�ж��ļ��Ƿ�����Լ��Ƿ����2M
            if (finfo.Exists && finfo.Length > 2048000)
            {
                finfo.Delete();
            }
            /**/
            ///����ֻд�ļ���
            using (System.IO.FileStream fs = finfo.OpenWrite())
            {
                /**/
                ///�������洴�����ļ�������д������
                System.IO.StreamWriter w = new System.IO.StreamWriter(fs);
                //w.Encoding = Encoding.UTF8;
                /**/
                ///����д����������ʼλ��Ϊ�ļ�����ĩβ
                w.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                w.WriteLine(sInfo, Encoding.UTF8);
                //��ջ��������ݣ����ѻ���������д�������
                w.Flush();
                /**/
                ///�ر�д������
                w.Close();
            }
        }

        /****************************/
    }
}
