using System;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using EP.Commons.Rest.Extension;
using EP.Commons.Rest;
using Castle.DynamicProxy;

namespace EP.Commons.ConfigClient
{
    public class ArbitraryRestClient<TInterface> : DynamicObject
    {
        private GeneralRestClient _generalRestClient;
        private string _serviceName;

        public ArbitraryRestClient()
        {
            HttpClient _httpClient;
            _serviceName = typeof(TInterface).Name.TrimStart('I').Replace("appservice", "", StringComparison.OrdinalIgnoreCase);

            if (typeof(TInterface).IsInterface)
            {
                var attrs = typeof(TInterface).GetCustomAttributes(false);
                var baseUriOrServiceName = attrs.First(at => at.GetType() == typeof(BaseUriAttribute)) as BaseUriAttribute;
                if (baseUriOrServiceName.IsServiceName) _httpClient = RestClientFactory.GetOrAddLoadBalancedClient(baseUriOrServiceName.BaseUriOrServiceName);
                else
                    _httpClient = RestClientFactory.CreateWithBaseAddress(baseUriOrServiceName.BaseUriOrServiceName);
                _generalRestClient = new GeneralRestClient() { HttpClient = _httpClient }.WithJwt().WithTenency().WithConfigCenterKey();
            }
        }


        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var path = $"api/services/app/{_serviceName}/{binder.Name}";

            result = ConventionallyIssue(path, binder.Name, args);
            this._result = result;
            return true;
        }

        protected object _result;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = _result;
            return true;
        }


        // Converting an object to a specified type.
        public override bool TryConvert(
            ConvertBinder binder, out object result)
        {
            var pg = new ProxyGenerator();

            var tp = typeof(TInterface);

            var ret = pg.CreateInterfaceProxyWithoutTarget(tp);

            // Converting to TInterface. 
            if (binder.Type == tp)
            {
                result = ret;
                return true;
            }


            // In case of any other type, the binder 
            // attempts to perform the conversion itself.
            // In most cases, a run-time exception is thrown.
            return base.TryConvert(binder, out result);
        }



        protected string ConventionallyIssue(string path, string methodName, object[] args)
        {
            HttpMethod httpMethod = GetHttpMethodTypeByConvension(methodName);
            HttpContent content;
            if (httpMethod == HttpMethod.Get) content = args[0]?.ToFormUrlEncodedContent();
            else content = args[0]?.ToJsonStringContent();
            var httpMessage = new HttpRequestMessage(httpMethod, path)
            {
                Content = content
            };
            var ret = "";
            this.SafelyRun(() =>
            {
                var retr = this.Sync(_generalRestClient.HttpClient.SendAsync(httpMessage));
                ret = this.Sync(retr.Content.ReadAsStringAsync());
            });
            return ret;
        }

        private HttpMethod GetHttpMethodTypeByConvension(string methodName)
        {
            var lowerMN = methodName.ToLower();
            if (lowerMN.StartsWith("get"))
                return HttpMethod.Get;
            if (lowerMN.StartsWith("pu"))
                return HttpMethod.Put;
            if (lowerMN.StartsWith("po"))
                return HttpMethod.Post;
            if (lowerMN.StartsWith("del"))
                return HttpMethod.Delete;
            return HttpMethod.Post;
        }



    }


}
