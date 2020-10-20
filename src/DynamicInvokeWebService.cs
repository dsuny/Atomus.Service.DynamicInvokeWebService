using Atomus.Diagnostics;
using Newtonsoft.Json;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Web.Services.Description;
using System.Web.Services.Protocols;

namespace Atomus.Service
{
    public class DynamicInvokeWebService : IService
    {
        private static Dictionary<string, WebServiceObject> WebServiceDictionary = new Dictionary<string, WebServiceObject>();
        private static System.Threading.Mutex Mutax = new System.Threading.Mutex();
        private readonly int serviceTimeout;

        public DynamicInvokeWebService()
        {
            try
            {
                this.serviceTimeout = this.GetAttributeInt("ServiceTimeout");
            }
            catch (Exception exception)
            {
                DiagnosticsTool.MyTrace(exception);
                this.serviceTimeout = 60000;
            }
        }

        Response IService.Request(ServiceDataSet serviceDataSet)
        {
            //IService service;

            try
            {
                if (!serviceDataSet.ServiceName.Equals("Atomus.Service.DynamicInvokeWebService"))
                    throw new Exception("Not Atomus.Service.DynamicInvokeWebService");

                ((IServiceDataSet)serviceDataSet).CreateServiceDataTable();

                return this.Excute(serviceDataSet);
            }
            catch (AtomusException exception)
            {
                DiagnosticsTool.MyTrace(exception);
                return (Response)Factory.CreateInstance("Atomus.Service.Response", false, true, exception);
            }
            catch (Exception exception)
            {
                DiagnosticsTool.MyTrace(exception);
                return (Response)Factory.CreateInstance("Atomus.Service.Response", false, true, exception);
            }
        }

        private Response Excute(ServiceDataSet serviceDataSet)
        {
            WebServiceObject webServiceObject;
            MethodInfo _MethodInfo;
            DataTable outPutTable;
            DataRow outPutDataRow;
            IResponse response;

            try
            {
                outPutTable = new DataTable("OutPutTable");
                outPutTable.Columns.Add("JSON", Type.GetType("System.String"));

                for (int i = 0; i < ((IServiceDataSet)serviceDataSet).Count; i++)
                {
                    webServiceObject = GetWebServiceObject(((IServiceDataSet)serviceDataSet)[i].ConnectionName, this.serviceTimeout);//serviceName=((IServiceDataSet)serviceDataSet)[i].ConnectionName
                    _MethodInfo = webServiceObject.Object.GetType().GetMethod(((IServiceDataSet)serviceDataSet)[i].CommandText);//methodName=((IServiceDataSet)serviceDataSet)[i].ConnectionName

                    foreach (DataRow dataRow in ((IServiceDataSet)serviceDataSet)[i].Rows)
                    {
                        var aa = JsonConvert.DeserializeObject((string)dataRow["JSON"]
                            , webServiceObject.Object.GetType().GetMethods()[0].GetParameters()[0].ParameterType
                            , new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Include,
                                MissingMemberHandling = MissingMemberHandling.Error,
                                Formatting = Formatting.None,
                                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                                FloatParseHandling = FloatParseHandling.Decimal
                            });
                        ;

                        outPutDataRow = outPutTable.NewRow();
                        outPutDataRow["JSON"] = JsonConvert.SerializeObject(_MethodInfo.Invoke(webServiceObject.Object, new object[] { aa }));
                        outPutTable.Rows.Add(outPutDataRow);
                    }
                }

                response = (IResponse)Factory.CreateInstance("Atomus.Service.Response", false, true);
                response.DataSet.Tables.Add(outPutTable);
                response.Status = Status.OK;

                return (Response)response;
            }
            catch (AtomusException exception)
            {
                DiagnosticsTool.MyTrace(exception);
                return (Response)Factory.CreateInstance("Atomus.Service.Response", false, true, exception);
            }
            catch (Exception exception)
            {
                DiagnosticsTool.MyTrace(exception);
                return (Response)Factory.CreateInstance("Atomus.Service.Response", false, true, exception);
            }
        }
        

        private static WebServiceObject GetWebServiceObject(string serviceName, int serviceTimeout)
        {
            WebServiceInfo webServiceInfo;
            object _Object;
            CompilerResults compilerResults;
            SoapHttpClientProtocol soapHttpClientProtocol;

            webServiceInfo = WebServiceInfo.GetWebServiceInfo(serviceName, serviceTimeout);

            _Object = null;

            try
            {
                Mutax.WaitOne();// 멀티쓰레딩

                if (!WebServiceDictionary.ContainsKey(webServiceInfo.ServiceAddress))
                {
                    compilerResults = CreateCompilerResults(webServiceInfo.ServiceAddress, webServiceInfo.ServiceUserName, webServiceInfo.ServicePassword);

                    if (compilerResults != null)
                    {
                        _Object = compilerResults.CompiledAssembly.CreateInstance(webServiceInfo.ServiceClassName);

                        soapHttpClientProtocol = (SoapHttpClientProtocol)_Object;
                        soapHttpClientProtocol.Credentials = new System.Net.NetworkCredential(webServiceInfo.ServiceUserName, webServiceInfo.ServicePassword);
                        soapHttpClientProtocol.PreAuthenticate = true;//사전 인증을 활성화
                        soapHttpClientProtocol.EnableDecompression = true;//압축 해제
                        soapHttpClientProtocol.UnsafeAuthenticatedConnectionSharing = true;//클라이언트가 NTLM 인증을 사용하여 XML Web services가 호스팅되는 웹 서버에 연결하는 데 연결 공유
                        soapHttpClientProtocol.Timeout = webServiceInfo.ServiceTimeout;

                        //서비스객체, CompilerResults, 처음 호출여부
                        WebServiceDictionary.Add(webServiceInfo.ServiceAddress
                            , new WebServiceObject()
                            {
                                Object = _Object,
                                CompilerResults = compilerResults,
                                //IsFirstExecute = false,
                                WebServiceInfo = webServiceInfo
                            });
                    }
                    else
                        throw new Exception(serviceName + " 서비스 생성 실패");
                }

                return WebServiceDictionary[webServiceInfo.ServiceAddress];
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Mutax.ReleaseMutex();// 멀티쓰레딩 
            }
        }

        /// <summary>
        /// 웹서비스 컴파일 결과 가져 오기
        /// </summary>
        /// <param name="_Address"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private static CompilerResults CreateCompilerResults(string _Address, string userName, string password)
        {
            ServiceDescriptionImporter _ServiceDescriptionImporter;
            CompilerResults _CompilerResults;
            CodeCompileUnit _CodeCompileUnit;
            CodeDomProvider _CodeDomProvider;
            string[] _AssemblyReferences;
            CompilerParameters _CompilerParameters;

            _CodeCompileUnit = new CodeCompileUnit();
            _CodeCompileUnit.Namespaces.Add(new CodeNamespace());

            // LOAD THE DOM /////////
            _ServiceDescriptionImporter = CreateServiceDescriptionImporter(_Address, userName, password);

            //Import the service into the Code-DOM tree. This creates proxy code that uses the service.
            //If zero then we are good to go
            if (_ServiceDescriptionImporter.Import(_CodeCompileUnit.Namespaces[0], _CodeCompileUnit) == 0)
            {
                //Compile the assembly proxy with the appropriate references
                _AssemblyReferences = new string[5] { "System.dll", "System.Web.Services.dll", "System.Web.dll", "System.Xml.dll", "System.Data.dll" };

                _CompilerParameters = new CompilerParameters(_AssemblyReferences);
                _CompilerParameters.GenerateInMemory = true;

                //Generate the proxy code
                _CodeDomProvider = CodeDomProvider.CreateProvider("CSharp");
                //_CodeDomProvider = CodeDomProvider.CreateProvider("VisualBasic");
                _CompilerResults = _CodeDomProvider.CompileAssemblyFromDom(_CompilerParameters, _CodeCompileUnit);

                //Check For Errors
                if (_CompilerResults.Errors.Count > 0)
                {
                    foreach (CompilerError _CompilerError in _CompilerResults.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine("========Compiler error============");
                        System.Diagnostics.Debug.WriteLine(_CompilerError.ErrorText);
                    }

                    throw new Exception("Compile Error Occured calling webservice. Check Debug ouput window.");
                }

                return _CompilerResults;
            }
            else
                throw new Exception("Service Description Import Error.");
        }

        /// <summary>
        /// 서비스 요청 주소에 대한 wsdl을 가져와 ServiceDescription으로 Service Description Importer를 생성하고 동적으로 컴파일해서 결과를 가져온다.
        /// </summary>
        /// <param name="_Address"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private static ServiceDescriptionImporter CreateServiceDescriptionImporter(string _Address, string userName, string password)
        {
            ServiceDescriptionImporter _ServiceDescriptionImporter;
            ServiceDescription _ServiceDescription;

            //Initialize a Code-DOM tree into which we will import the service.
            _ServiceDescription = GetServiceDescription(_Address, userName, password);

            //Initialize a service description importer.
            _ServiceDescriptionImporter = new ServiceDescriptionImporter();
            //_ServiceDescriptionImporter.ProtocolName = "Soap12"

            //Use SOAP 1.2.
            _ServiceDescriptionImporter.AddServiceDescription(_ServiceDescription, null, null);

            //Generate a proxy client.
            _ServiceDescriptionImporter.Style = ServiceDescriptionImportStyle.Client;

            //Generate properties to represent primitive values.
            _ServiceDescriptionImporter.CodeGenerationOptions = System.Xml.Serialization.CodeGenerationOptions.GenerateProperties;

            return _ServiceDescriptionImporter;
        }

        /// <summary>
        /// Create Service Description Importer
        /// </summary>
        /// <param name="_Address"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private static ServiceDescription GetServiceDescription(string _Address, string userName, string password)
        {
            System.Net.WebClient _WebClient;
            System.IO.Stream _Stream;
            ServiceDescription _ServiceDescription;

            _WebClient = null;
            _Stream = null;
            _ServiceDescription = null;

            try
            {
                //Connect To the web service
                _WebClient = new System.Net.WebClient();
                _WebClient.Credentials = new System.Net.NetworkCredential(userName, password);
                _WebClient.Headers.Set(System.Net.HttpRequestHeader.ContentType, "application/gzip");
                _Stream = _WebClient.OpenRead(_Address);

                //Now read the WSDL file describing a service.
                _ServiceDescription = ServiceDescription.Read(_Stream);
            }
            finally
            {
                _Stream?.Close();
                _WebClient?.Dispose();
            }

            return _ServiceDescription;
        }
    }
}
