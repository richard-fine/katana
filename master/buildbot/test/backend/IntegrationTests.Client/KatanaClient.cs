using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace Unity.Katana.IntegrationTests.Client
{
    public class KatanaClient
    {
        private static readonly HttpClient client;
        private static HttpClientHandler handler;
        //private JObject settings = JObject.Parse("katana.settings.json");

        public string url { get; set; }

        static KatanaClient()
        {
            handler = new HttpClientHandler();            
            client = new HttpClient(handler);
        }
                            
        public void SetBaseAddress(string url)
        {
            if(client.BaseAddress == null) {
                client.BaseAddress = new Uri(url);
            }
        }


        public async Task<HttpResponseMessage> SendPostRequest(string url, string content, string contentType)
        {            
            var httpcontent = new StringContent(content, Encoding.UTF8, contentType);
            HttpResponseMessage response = await client.PostAsync(url, httpcontent);
            return response;
        }
        #region predefined queries
        public async Task<HttpResponseMessage> GetAllBuilders()
        {
            SetContentType(ContentType.Json);
            var url = @"/json/builders/";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetABuilderInfo(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetCachedBuildsOfABuilder(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetAllBuildsOfABuilder(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds/all";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="num"> either positive, a build number, or negative,a past build.Using &lt4 will give the last 4  builds.</param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetABuildOfABuilder(string builder, int num)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds/{num.ToString()}";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetLatestChangesOfABuilder(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds/-1/sourcestamp/changes";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        /// <summary>vb
        /// //Builds without properties or steps
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetBasicInfosOfBuilds(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds?props=0&steps=0";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetLastXBuilds(string builder, int X)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds?" + RepeatQuery("select={0}", X);
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetChangesOfLastXBuilds(string builder, int X)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds?" + RepeatQuery("select={0}/source_stamp/changes", X);
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetSlavesOfBuilder(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/slaves?buildsteps=0&buildprops=0";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetStartSlavesOfBuilder(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/startslaves?buildsteps=0&buildprops=0";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetBuilderAndItsSlaveInfo(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}?select=&select=slaves";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetSlaveInfo(string slave)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/slaves/{slave}?buildsteps=0&buildprops=0";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetBuildsOnSlave(string slave)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/slaves/{slave}/builds";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetBuilderOnSlave(string slave)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/slaves/{slave}/builders";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetLastXBuildsOnSlave(string slave, int num)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/slaves/{slave}/builds/<{num.ToString()}?buildsteps=1&buildprops=1";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetAllProjects()
        {
            SetContentType(ContentType.Json);
            var url = $"/json/projects/";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetAProjects(string project)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/projects/{project}";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetBuilderofProject(string project, string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/projects/{project}/{builder}?buildsteps=0&buildprops=0";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetBuildQueue()
        {
            SetContentType(ContentType.Json);
            var url = @"/json/buildqueue/";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetPendingBuilds(string builder)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/pending/%s/";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public async Task<HttpResponseMessage> GetGlobalStatus()
        {
            SetContentType(ContentType.Json);
            var url = @"/json/globalstatus/";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        /// <summary>
        /// Return json of a build - cached to help debugging
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="buildNumber"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetBuild(string builder, int buildNumber)
        {
            SetContentType(ContentType.Json);
            var url = $"/json/builders/{builder}/builds/{buildNumber.ToString()}";
            HttpResponseMessage response = await client.GetAsync(url);
            return response;
        }

        public int GetBuildNumberFromRevision(string revision, string builder, int num = 3)
        {
            int buildnr = -1;
            var resp = GetLastXBuilds(builder, num);
            var content = JObject.Parse(resp.Result.Content.ReadAsStringAsync().Result);            
            for (int i = 1; i <= num; i++)
            {
                string iStr = (i * -1).ToString();
                string rev = content[iStr]["sourceStamps"][0]["revision"].ToString();
                
                if (rev == revision)
                {
                    buildnr = Convert.ToInt16(content[iStr]["number"].ToString());
                    break;
                }                
            }
            return buildnr;
        }

                
        #endregion


        #region Predefine actions
        public async Task<HttpResponseMessage> LaunchBuild(string project,
                                                   string builder,
                                                   string branch,
                                                   string revision,
                                                   string priority,
                                                   string slave = null,
                                                   bool force = true)
        {
            string action = $"/projects/{project}/builders/{builder}/force?{project.ToLower()}_branch={branch}" +
                                $"&returnpage=pending_json";

            var slctslave = slave == null ? "default" : slave;
            List<string> payload = new List<string>();
            payload.Add("forcescheduler=unityBuildersWithSmartSelect [force]");
            payload.Add($"selected_slave={slctslave}");
            payload.Add($"priority={priority}");
            payload.Add("reason=IntegrationTest");
            payload.Add($"unity_revision={revision}");
            if (force)
            {
                payload.Add("checkbox=force_rebuild");
            }

            string content = string.Join("&", payload);
            HttpResponseMessage response = await SendPostRequest(action, content, ContentType.wwwForm);
            return response;
        }

        public async Task<HttpResponseMessage> StopBuild(string project, string builder, string build, string branch)
        {
            string action = $"/projects/{project}/builders/{builder}/builds/{build}/stopchain?" +
                $"{project.ToLower()}_branch={branch}";
            string content = "comments=Backend Integration Test";
            HttpResponseMessage response = await SendPostRequest(action, content, ContentType.wwwForm);
            return response;
        }

        public async Task<HttpResponseMessage> StopBuildChain(string project, string builder, 
                                                                string build, string branch)
        {
            string action = $"/projects/{project}/builders/{builder}/builds/{build}/stopchain?" +
                $"{project.ToLower()}_branch={branch}";
            string content = "comments=Backend Integration Test";
            HttpResponseMessage response = await SendPostRequest(action, content, ContentType.wwwForm);
            return response;
        }

        #endregion
        /// <summary>
        /// In order to implement the query which can retrieve information of last X times
        /// Generate the query by repeat the base 'query' by 'X' times,
        /// the parameter is increase from 1 to X or decrease from -1 to -X        
        /// </summary>
        /// <param name="query">the base query in string.Format() format</param>
        /// <param name="num">how many times to repeat</param>
        /// <param name="minus">Use positive or negitive number, default is true, means use negitive number</param>
        /// <returns>the query string</returns>
        /// <example> 
        /// RepeatQuery("select={0}", 3)  returns select = -1 & select = -2 & select = -3 
        /// RepeatQuery("select={0}", 3, false)  returns select = 1 & select = 2 & select = 3
        /// </example>
        private string RepeatQuery(string query, int num, bool minus = true)
        {
            var baseQuery = query;
            var mul = minus ? -1 : 1;
            for (int i = 1; i <= num; i++)
            {
                if (i == 1)
                {
                    query = string.Format(baseQuery + "&", (i * mul));
                }
                else if (i == num)
                {
                    query = query + string.Format(baseQuery, (i * mul));
                }
                else
                {
                    query = query + string.Format(baseQuery + "&", (i * mul));
                }
            }

            return query;            
        }

        private void SetContentType(string type)
        {
            client.DefaultRequestHeaders
                    .Accept
                    .Add(new MediaTypeWithQualityHeaderValue(type));
        }

        public void Dispose()
        {
            client.Dispose();              
        }


    }
}
