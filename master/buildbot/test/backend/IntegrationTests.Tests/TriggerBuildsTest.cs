using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Unity.Katana.IntegrationTests.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Unity.Katana.IntegrationTests.Tests
{
    public class TriggerBuildsTest : IntegrationTestsBase
    {        

        [Fact]
        public async Task TriggerBuildTest()
        {
            TextWriterTraceListener myTextListener = null;
            Stream myFileName = File.Create("TriggerBuildTest.log");
            StreamWriter myOutputWriter = new StreamWriter(myFileName);
            myTextListener = new TextWriterTraceListener(myOutputWriter);
            Trace.Listeners.Add(myTextListener);

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            var client = new KatanaClient();
            client.SetBaseAddress(settings["BaseAddress"].ToString());
            var setup = settings["TestSetup"].Where( x => (string)x["name"] == "default").ToArray().First();
            var project = setup["project"].ToString();
            var builder = setup["builder"].ToString();
            var branch = setup["branch"].ToString();
            //// launch 2 setup builds to make the slave busy  ////
            await client.LaunchBuild(project, builder, branch, "10000001", "99");
            await client.LaunchBuild(project, builder, branch, "10000002", "99");
            Trace.WriteLine("Two setup builds are launched");
            Thread.Sleep(3000);
            //// 
            await client.LaunchBuild(project, builder, branch, "11111111", "10");
            await client.LaunchBuild(project, builder, branch, "99999999", "90");
            await client.LaunchBuild(project, builder, branch, "44444444", "40");
            Trace.WriteLine("Three setup builds are launched");

            bool isAllRunning = false;
            int counter = 0;
            int build1 = -1;
            int build2 = -1;
            int build3 = -1;

            var resp = await client.GetAllBuilders();
            var content = resp.Content.ReadAsStringAsync().Result;            
            while (!isAllRunning)
            {
                counter++;
                if (counter > 60)
                    Assert.True(false, "Testcase failed after running for 5 minutes");

                    //// Stop the setup builds if they are running ////
                build1 = client.GetBuildNumberFromRevision("10000001", builder);
                build2 = client.GetBuildNumberFromRevision("10000002", builder);
                if (build1 >= 0)
                {
                    await client.StopBuild(project, builder, build1.ToString(), branch);
                }
                if (build2 >= 0)
                {
                    await client.StopBuild(project, builder, build2.ToString(), branch);
                }
                

                //// Read the build number ////
                build1 = client.GetBuildNumberFromRevision("11111111", builder);
                build2 = client.GetBuildNumberFromRevision("99999999", builder);
                build3 = client.GetBuildNumberFromRevision("44444444", builder);

                isAllRunning = (build1 >= 0) && (build2 >= 0) && (build3 >= 0);

                if (!isAllRunning)
                    Trace.WriteLine($"not all the builds are running, {build1}, {build2} {build3}, " +
                        $"refresh after 5 seconds");
                Thread.Sleep(5000);
            }

            client.Dispose();
            myTextListener.Flush();
            Trace.Listeners.Remove(myTextListener);
            myTextListener.Dispose();
            Assert.True(build1 > build3 && build3 > build2);
        }

        private void CreateBaseConfigFile(string config)
        {
            var assembly = GetType().GetTypeInfo().Assembly;
            string resourceKey = $"IntegrationTests.Tests.{config}";
            using (Stream resource = assembly.GetManifestResourceStream(resourceKey))
            {
                using (FileStream output = new FileStream(config, FileMode.Create))
                {
                    resource.CopyTo(output);
                }
            }
        }

    }
}
