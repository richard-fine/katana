using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Unity.Katana.IntegrationTests.Client;
using Unity.Katana.IntegrationTests.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FluentAssertions;


namespace Unity.Katana.IntegrationTests.Tests
{
    public class TriggerBuildsTest : IntegrationTestsBase
    {
        /// <summary>
        /// Trigger three builds with different priority
        /// Verify that the execution order follows the priority
        /// </summary>        
        [Fact]
        public async Task TriggerBuildTest()
        {
            #region arrange
            TextWriterTraceListener myTextListener = SetupTraceFile("TriggerBuildTest.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));            
            client.SetBaseAddress(settings["BaseAddress"].ToString());
            var setup = settings["TestSetup"].Where( x => (string)x["name"] == "default").ToArray().First();
            var project = setup["project"].ToString();
            var builder = setup["builder"].ToString();
            var branch = setup["branch"].ToString();
            #endregion

            #region action
            //// launch 2 setup builds to make the slave busy  ////
            await client.LaunchBuild(project, builder, branch, "10000001", "99");
            await client.LaunchBuild(project, builder, branch, "10000002", "99");
            Trace.WriteLine("Two setup builds are launched");
            Thread.Sleep(3000);
            //// launch 3 test builds. ////
            await client.LaunchBuild(project, builder, branch, "11111111", "10");
            await client.LaunchBuild(project, builder, branch, "99999999", "90");
            await client.LaunchBuild(project, builder, branch, "44444444", "40");
            Trace.WriteLine("Three setup builds are launched");

            bool isAllRunning = false;
            int counter = 0;
            int build1 = -1;
            int build2 = -1;
            int build3 = -1;
            
            //// Wait until all the test build is running ////
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
#endregion

            #region clean up and assertion
            client.Dispose();
            WriteTraceToFile(myTextListener);

            (build1 > build3 && build3 > build2).Should().BeTrue("The build number should be ordered " +
                "accodring to their priority");
#endregion
        }

        /// <summary>
        /// Launch 3 build using same build, make sure the builds do not use other free slave
        /// Precondition: There are more than one slave free during test.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task UseSpecifiedSlaveTest()
        {
            #region arrange
            List<string> errmsgs = new List<string>();
            TextWriterTraceListener myTextListener = SetupTraceFile("UseSpecifiedSlaveTest.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            client.SetBaseAddress(settings["BaseAddress"].ToString());
            var setup = settings["TestSetup"].Where(x => (string)x["name"] == "UseSpecifiedSlaveTest")
                         .ToArray().First();
            var project = setup["project"].ToString();
            var builder = setup["builder"].ToString();
            var branch = setup["branch"].ToString();
            #endregion
            #region action
            //// Step : Get the list of available 'build slaves' ////
            var resp = await client.GetSlavesOfBuilder(builder);            
            var content = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
            List<string> freeSlaves = new List<string>();
            foreach (var slave in content)
            {
                JObject _slave = (JObject)slave.Value;
                if ((bool)_slave["connected"] )
                { 
                    if (_slave["runningBuilds"] == null || _slave["runningBuilds"].Count() == 0)
                    {
                        freeSlaves.Add(_slave["name"].ToString());
                        Trace.WriteLine($"Build slave: {_slave["name"].ToString()} is free");
                    }
                    else
                    {
                        for (int i = 0; i < _slave["runningBuilds"].Count(); i++)
                        {
                            //// If there are not free slaves, Stop the builds on all the slaves ////
                            string url = _slave["runningBuilds"][i]["builder_url"].ToString();
                            url = url.Replace("?", "/stop?");
                            await client.StopBuild(url);
                            Trace.WriteLine($"Try to stop the build on {_slave["name"].ToString()}");
                        }

                        Thread.Sleep(5000);

                        if (!(_slave["runningBuilds"] == null || _slave["runningBuilds"].Count() == 0))
                        {
                            freeSlaves.Add(_slave["name"].ToString());
                            Trace.WriteLine($"No build on build slave: {_slave["name"].ToString()}, which is free now");
                        }
                        else
                        {
                            Trace.WriteLine($"Build slave: {_slave["name"].ToString()} is not stoppable.");
                        }
                    }
                }
                else
                {
                    Trace.WriteLine($"Build slave: {_slave["name"].ToString()} is offline");
                }
            }

            //// Step: Get the list of 'processing slaves' ////
            resp = await client.GetStartSlavesOfBuilder(builder);
            content = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
            List<string> processingSlaves = new List<string>();
            foreach (var slave in content)
            {
                JObject _slave = (JObject)slave.Value;
                if ((bool)_slave["connected"])
                {
                    processingSlaves.Add(_slave["name"].ToString());
                    Trace.WriteLine($"Process Slave {_slave["name"].ToString()} is online");
                }
                else
                {
                    Trace.WriteLine($"Process Slave {_slave["name"].ToString()} is offline");
                }
            }
                        
            //// Step : Launch 3 builds on the first build slave ////
            List<string> revisions = new List<string>() { "222564a95d9a" };
            for (int i = 0; i < 3; i++)
            {
                await client.LaunchBuild(project, builder, branch, revisions[0], "90", freeSlaves.First());
                Trace.WriteLine($"A build on builder {builder} of project {project} - branch {branch} is launched");
            }
                
            //// Step : Wait all the builds are running ,and read their build number ////
            List<int> builds = WaitAllBuildsAreRunning(revisions, client, builder);
            foreach (var item in builds)
            {
                Trace.WriteLine($"A build is/was running with builder number {item}");
            }


            #endregion
            #region clean up and assertion

            //// Step: Check the builds is either running by select 'build slave'
            ////       or by a available 'processing slaves' in the list////
            //// Remark: If a correct revision is provide, the build slave is used, otherwise processing slave is used"
            foreach (var b in builds)
            {
                resp = await client.GetBuild(builder, b);
                content = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                var _slave = content["slave"].ToString();
                Trace.WriteLine($"build number {b} used slave : {_slave} to build");
                bool _isContained = processingSlaves.Contains(_slave) || _slave == freeSlaves.First();
                _isContained. Invoking(t=>t.Should().
                     BeTrue("Builder should use a free slave either a build slave or a processing slaves"))
                    .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                    .AddResult(errmsgs);
                                
            }

            //// Step: Read the information of the selected build slave, Check all 3 builds are there////
            resp = await client.GetLastXBuildsOnSlave(freeSlaves.First(), 3);
            var contentArray = JArray.Parse(resp.Content.ReadAsStringAsync().Result);
            contentArray.Count.Invoking(t => t.Should().Be(3, "only 3 builds should be fetched"))
                .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                .AddResult(errmsgs);

            foreach (JObject item in contentArray)
            {
                var _rev = item["sourceStamps"][0]["revision_short"].ToString();
                var _num = (int)item["number"];
                Trace.WriteLine($"Build #{_num} used revision {_rev}");
                ///// Step : Check the last 3 builds on that slave used the revision provide early in the testcase. ////
                bool _isContained = revisions.Contains(_rev);
                _isContained.Invoking(t => t.Should()
                    .BeTrue($"build with rev:{_rev} should be built by slave {freeSlaves.First()}"))
                    .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                    .AddResult(errmsgs);

                ///// Step : Check the last 3 builds on that slave contains 
                ////         the build number we get in early in the testcase. ////
                _isContained = builds.Contains(_num);
                _isContained.Invoking(t => t.Should()
                    .BeTrue($"build #{_num} should be built by this slave {freeSlaves.First()}"))
                    .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                    .AddResult(errmsgs);

                if (_isContained)
                {
                    builds.Remove(_num);
                }
            }

            builds.Count.Invoking(t => t.Should().Be(0, "3 predefined revision number should be removed in last step"))
                    .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                    .AddResult(errmsgs);

            client.Dispose();
            WriteTraceToFile(myTextListener);
            AssertTestcase(errmsgs);
            #endregion
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
