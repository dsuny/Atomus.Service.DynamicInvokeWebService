using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atomus.Service
{
    public class WebServiceInfo
    {
        public string ServiceAddress { get; set; }
        public string ServiceClassName { get; set; }
        public string ServiceUserName { get; set; }
        public string ServicePassword { get; set; }
        public int ServiceTimeout { get; set; } = 60000;

        private static System.Threading.Mutex mtx = new System.Threading.Mutex();
        private static Dictionary<string, WebServiceInfo> WebServiceInfoDictionary = new Dictionary<string, WebServiceInfo>();

        public static WebServiceInfo GetWebServiceInfo(string serviceName, int serviceTimeout)
        {
            WebServiceInfo webServiceInfo;
            string connectionString;

            try
            {
                if (WebServiceInfoDictionary.ContainsKey(serviceName))
                    return WebServiceInfoDictionary[serviceName];

                webServiceInfo = new WebServiceInfo();
                webServiceInfo.ServiceTimeout = serviceTimeout;

                if (ConfigurationManager.AppSettings[string.Format("WebService.{0}", serviceName)] == null)
                    throw new AtomusException(string.Format("연결 문자열 WebService.{0}가 없습니다.", serviceName));

                connectionString = ConfigurationManager.AppSettings[string.Format("WebService.{0}", serviceName)];

                //< add key = "WebService.기간오픈" value = "User ID=IF_KIIFOB;Password=Interface!12;ClassName=SI_GRP_PP0060_SOService;Address=http://klneai.kolon.com:50000/dir/wsdl?p=ic/397ef8706af13c5d8f9ff5b43ec30f46" />
                //connectionString = "User ID=IF_KIIFOB;Password=Interface!12;ClassName=SI_GRP_PP0060_SOService;Address=http://klneai.kolon.com:50000/dir/wsdl?p=ic/397ef8706af13c5d8f9ff5b43ec30f46";


                var cs = (from a in connectionString.Split(';')
                          where a.Contains("User ID")
                          select a);

                if (cs != null && cs.Count() == 1)
                {
                    var ic = cs.ToList()[0].Split('=');

                    if (ic != null && ic.Length == 2)
                        webServiceInfo.ServiceUserName = ic[1].Trim();
                }

                cs = (from a in connectionString.Split(';')
                      where a.Contains("Password")
                      select a);

                if (cs != null && cs.Count() == 1)
                {
                    var ic = cs.ToList()[0].Split('=');

                    if (ic != null && ic.Length == 2)
                        webServiceInfo.ServicePassword = ic[1].Trim();
                }

                cs = (from a in connectionString.Split(';')
                      where a.Contains("ClassName")
                      select a);

                if (cs != null && cs.Count() == 1)
                {
                    var ic = cs.ToList()[0].Split('=');

                    if (ic != null && ic.Length == 2)
                        webServiceInfo.ServiceClassName = ic[1].Trim();
                }

                cs = (from a in connectionString.Split(';')
                      where a.Contains("Address")
                      select a);

                if (cs != null && cs.Count() == 1)
                    webServiceInfo.ServiceAddress = cs.ToList()[0].Substring(cs.ToList()[0].IndexOf('=') + 1);

                WebServiceInfoDictionary.Add(serviceName, webServiceInfo);

                return webServiceInfo;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}