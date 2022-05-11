using System;
using System.Collections.Generic;
using System.Text;
using System.Data.OracleClient;
using System.Data;

namespace PRT.GDSMPay.Common
{
    public static class OracleHelper
    {
        public const string ostr = "gdsmpay";
        public const string ustr = "gdsmpay";
        public const string pstr = "gdsmpay";

        public static string connectionString = "Data Source=" + ostr + ";Persist Security Info=True;User ID=" + ustr + ";Password=" + pstr + ";Unicode=True;Pooling=true;Min Pool Size=50;Max Pool Size=500;Connection Lifetime=1;";   //Connection Lifetime=10000;
        private static OracleConnection sqlConn = null;

        private static OracleConnection SqlConn
        {
            get
            {
                openCon();
                return sqlConn;
            }
        }

        public static OracleDataReader GetDataReader(string sql)
        {
            try
            {
                OracleCommand sqlCmd = new OracleCommand();
                sqlCmd.CommandText = sql;
                sqlCmd.Connection = SqlConn;
                OracleDataReader sqlDr = sqlCmd.ExecuteReader(CommandBehavior.CloseConnection);
                sqlDr.Read();
                return sqlDr;
            }
            catch
            {
                return null;
            }
        }

        public static DataTable GetDataTable(string sql)
        {

            try
            {
                DataTable dt = new DataTable();
                OracleCommand sqlCmd = new OracleCommand();
                sqlCmd.CommandText = sql;
                sqlCmd.Connection = SqlConn;

                OracleDataAdapter da = new OracleDataAdapter(sqlCmd);
                da.Fill(dt);

                return dt;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                sqlConn.Close();
            }
        }

        public static int ExecSql(string sql)
        {
            try
            {
                OracleCommand sqlCmd = new OracleCommand();
                sqlCmd.CommandText = sql;
                sqlCmd.Connection = SqlConn;
                int i = sqlCmd.ExecuteNonQuery();
                return i;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                sqlConn.Close();
            }
        }

        public static int ExecSql(string sql,OracleConnection con)
        {
            try
            {
                OracleCommand sqlCmd = new OracleCommand();
                sqlCmd.CommandText = sql;
                sqlCmd.Connection = con;
                int i = sqlCmd.ExecuteNonQuery();
                return i;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static string getConfig(string sConfigStr)
        {
            OracleDataReader odr = null;
            try
            {
                odr = GetDataReader("select value from config where name='" + sConfigStr + "'");
                string sValue = odr.GetString(0);
                return sValue;
            }
            catch
            {
                return "";
            }
            finally
            {
                if (!odr.IsClosed)
                {
                    odr.Close();
                }
            }
        }

        private static void openCon()
        {
            try
            {
                if (sqlConn == null)
                {
                    sqlConn = new OracleConnection();
                    sqlConn.ConnectionString = "Data Source=" + ostr + ";User Id=" + ustr + ";Password=" + pstr + ";Persist Security Info=True;";
                    sqlConn.Open();
                }
                else if (sqlConn.State == ConnectionState.Closed)
                {
                    sqlConn.Open();
                }
            }
            catch
            {
            }
        }

        public static DataSet GetDataSet(string sql)
        {
            try
            {
                OracleDataAdapter oda = new OracleDataAdapter();
                DataSet ds = new DataSet();
                OracleCommand sqlcmd = new OracleCommand(sql, SqlConn);
                oda.SelectCommand = sqlcmd;
                oda.Fill(ds);
                return ds;
            }
            catch
            {
                return null;
            }

        }

        public static OracleConnection OracleCon()
        {
            OracleConnection conn = new OracleConnection();
            conn.ConnectionString = "Data Source=" + ostr + ";User Id=" + ustr + ";Password=" + pstr + ";Persist Security Info=True;";
            return conn;
        }
    }
}
