using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.HttpModels
{
    internal abstract class HttpModelBase
    {
        public HttpModelBase(HttpMethod method, string uri, byte uriCount)
        {
            Method = method;
            Uri = uri;
            UriParamsCount = uriCount;
        }

        [JsonIgnore]
        public HttpMethod Method { get; private set; }

        [JsonIgnore]
        private string Uri { get; set; }

        [JsonIgnore]
        private byte UriParamsCount { get; set; }

        [JsonIgnore]
        public bool WholeModel { get; private set; } = true;

        public string GetUri(params string[] uriParams)
        {
            if (UriParamsCount == 0)
                return Uri;
            if (uriParams.Length != UriParamsCount)
                throw new Exception("Invalid amout of parameters to form a URI");

            return string.Format(Uri, uriParams);
        }

        public void SetUri(HttpMethod method, string uri, byte uriCount, bool wholeModel)
        {
            Method = method;
            Uri = uri;
            UriParamsCount = uriCount;
            WholeModel = wholeModel;
        }
    }
}
