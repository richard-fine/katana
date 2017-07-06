using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            string testcasename = GetTestcaseName();
            TextWriterTraceListener myTextListener = SetupTraceFile($"{testcasename}.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            string _baseAddress = settings["BaseAddress"].ToString();
            client.SetBaseAddress(_baseAddress);
            Trace.WriteLine($"Set base address {_baseAddress}");
            var setup = settings["TestSetup"].Where( x => (string)x["name"] == testcasename).ToArray().First();
            var project = setup["project"].ToString();
            var builder = setup["builder"].ToString();
            var branch = setup["branch"].ToString();
            Trace.WriteLine($"Read parameter project : {project}, builder: {builder}, branch: {branch}");
            await FreeAllSlavesOfABuilder(client, builder);
            #endregion

            #region action
            //// launch 2 setup builds to make the slave busy  ////
            await client.LaunchBuild(project, builder, branch, "22490e60db9f", "99");
            await client.LaunchBuild(project, builder, branch, "a62ae33069cd", "99");
            Trace.WriteLine("Two setup builds are launched");
            Thread.Sleep(3000);
            //// launch 3 test builds. ////
            await client.LaunchBuild(project, builder, branch, "bf98c4809fcc", "10");
            await client.LaunchBuild(project, builder, branch, "a90e5d2c796d", "90");
            await client.LaunchBuild(project, builder, branch, "8982804a2d35", "40");
            Trace.WriteLine("Three setup builds are launched");

            bool isBuild1Stopped = false;
            bool isBuild2Stopped = false;
            int counter = 0;
            int build1 = -1;
            int build2 = -1;
            int build3 = -1;
            
            //// Wait until all the test build is running ////
            while (!isBuild1Stopped || !isBuild2Stopped)
            {                
                counter++;
                if (counter > 60)
                    Assert.True(false, "Testcase failed after running for 5 minutes");

                //// Stop the setup builds if they are running ////
                build1 = client.GetBuildNumberFromRevisionAndTime("22490e60db9f", builder, 0, 5);
                build2 = client.GetBuildNumberFromRevisionAndTime("a62ae33069cd", builder, 0, 30, 5);
                if (build1 >= 0)
                {
                    await client.StopBuild(project, builder, build1.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build1}");
                    Thread.Sleep(1000);
                    isBuild1Stopped = true;
                }
                if (build2 >= 0)
                {
                    await client.StopBuild(project, builder, build2.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build2}");
                    Thread.Sleep(1000);
                    isBuild2Stopped = true;
                }
                
                Thread.Sleep(5000);
            }

            isBuild1Stopped = false;
            isBuild2Stopped = false;
            bool isBuild3Stopped = false;
            counter = 0;
            while (!isBuild1Stopped || !isBuild2Stopped || !isBuild3Stopped)
            {
                counter++;
                if (counter > 60)
                    Assert.True(false, "Testcase failed after running for 5 minutes");
                //// Read the build number ////
                int _build1 = client.GetBuildNumberFromRevisionAndTime("bf98c4809fcc", builder, 0, 40, 5);
                int _build2 = client.GetBuildNumberFromRevisionAndTime("a90e5d2c796d", builder, 0, 20, 5);
                int _build3 = client.GetBuildNumberFromRevisionAndTime("8982804a2d35", builder, 0, 30, 5);

                if (_build1 >= 0)
                {
                    build1 = _build1;
                    await client.StopBuild(project, builder, build1.ToString(), branch);                    
                    Trace.WriteLine($"Stopping build {build1}");
                    Thread.Sleep(1000);
                    isBuild1Stopped = true;

                }
                if (_build2 >= 0)
                {
                    build2 = _build2;
                    await client.StopBuild(project, builder, build2.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build2}");
                    Thread.Sleep(1000);
                    isBuild2Stopped = true;
                }

                if (_build3 >= 0)
                {
                    build3 = _build3;
                    await client.StopBuild(project, builder, build3.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build3}");
                    Thread.Sleep(1000);
                    isBuild3Stopped = true;
                }
                                
                Trace.WriteLine($"builds number : {build1}, {build2}, {build3}");
                if (!isBuild1Stopped || !isBuild2Stopped || !isBuild3Stopped)
                    Trace.WriteLine("not all the builds are running,  refresh after 5 seconds");
                Thread.Sleep(5000);
            }
            #endregion

            #region clean up and assertion            
            WriteTraceToFile(myTextListener);
            //await client.StopAllBuildOnBuilder(project, builder, branch);
            (build1 > build3 && build3 > build2).Should().BeTrue($"The build number {build2}, {build3}, {build1} should be in assending ordered " +
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
            string testcasename = GetTestcaseName();
            List<string> errmsgs = new List<string>();
            TextWriterTraceListener myTextListener = SetupTraceFile($"{testcasename}.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            client.SetBaseAddress(settings["BaseAddress"].ToString());
            var setup = settings["TestSetup"].Where(x => (string)x["name"] == testcasename)
                         .ToArray().First();
            var project = setup["project"].ToString();
            var builder = setup["builder"].ToString();
            var branch = setup["branch"].ToString();
            var revision_short = setup["revision"].ToString();
            await FreeAllSlavesOfABuilder(client, builder);
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
            List<string> revisions = new List<string>() { revision_short };
            for (int i = 0; i < 3; i++)
            {
                await client.LaunchBuild(project, builder, branch, revisions[0], "90", freeSlaves.First());
                Trace.WriteLine($"A build on builder {builder} of project {project} - branch {branch} is launched");
            }
            Thread.Sleep(3000);

            //// Stop: The builds take long time to build, stop them. ////
            bool isPengingBuild = true;
            while (isPengingBuild)
            {
                await StopCurrentBuildsIfPending(client, project, builder, branch);
                var response = client.GetPendingBuilds(builder);
                JArray contents = JArray.Parse(response.Result.Content.ReadAsStringAsync().Result);
                if (contents.Count < 1)
                {
                    isPengingBuild = false;
                }
            }

            var _build = client.GetBuildNumberFromRevision(revision_short, builder, 1);
            await client.StopBuild(project, builder, _build.ToString(), branch);


            //// Step : Wait all the builds are running ,and read their build number ////            
            List<int> builds = client.GetXBuildNumberFromRevision(revision_short, builder, 3);

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

            ////// Step: Read the information of the selected build slave, Check all 3 builds are there////
            //resp = await client.GetLastXBuildsOnSlave(freeSlaves.First(), 3);
            //var contentArray = JArray.Parse(resp.Content.ReadAsStringAsync().Result);
            //contentArray.Count.Invoking(t => t.Should().Be(3, "only 3 builds should be fetched"))
            //    .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
            //    .AddResult(errmsgs);

            //foreach (JObject item in contentArray)
            //{
            //    var _rev = item["sourceStamps"][0]["revision_short"].ToString();
            //    var _num = (int)item["number"];
            //    Trace.WriteLine($"Build #{_num} used revision {_rev}");
            //    ///// Step : Check the last 3 builds on that slave used the revision provide early in the testcase. ////
            //    bool _isContained = revisions.Contains(_rev);
            //    _isContained.Invoking(t => t.Should()
            //        .BeTrue($"build with rev:{_rev} should be built by slave {freeSlaves.First()}"))
            //        .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
            //        .AddResult(errmsgs);

            //    ///// Step : Check the last 3 builds on that slave contains 
            //    ////         the build number we get in early in the testcase. ////
            //    _isContained = builds.Contains(_num);
            //    _isContained.Invoking(t => t.Should()
            //        .BeTrue($"build #{_num} should be built by this slave {freeSlaves.First()}"))
            //        .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
            //        .AddResult(errmsgs);                
            //}

            await client.StopAllBuildOnBuilder(project, builder, branch);
            WriteTraceToFile(myTextListener);
            AssertTestcase(errmsgs);
            #endregion
        }

        [Fact]
        public async Task RebuildTest()
        {
            #region arrange
            string testcasename = GetTestcaseName();
            List<string> errmsgs = new List<string>();
            TextWriterTraceListener myTextListener = SetupTraceFile($"{testcasename}.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            string _baseAddress = settings["BaseAddress"].ToString();
            client.SetBaseAddress(_baseAddress);
            Trace.WriteLine($"Set base address {_baseAddress}");
            var setup = settings["TestSetup"].Where(x => (string)x["name"] == testcasename).ToArray().First();
            var project = setup["project"].ToString();
            var builder = setup["builder"].ToString();
            var branch = setup["branch"].ToString();
            var revision = setup["revision"].ToString();
            Trace.WriteLine($"Read parameter project : {project}, builder: {builder}, branch: {branch}");
            await FreeAllSlavesOfABuilder(client, builder);
            int cnt = 0;
            #endregion
            #region action

            //// Step : search a successful build, search cached builds first, if no successful build
            ////        then search all builds. If still no successful build, launch a new build


            ///
            HttpResponseMessage resp = await client.GetCachedBuildsOfABuilder(builder);
            JArray buildsArray = ParseJArrayResponse(resp);
            int buildnr = -1;
            bool isSuccessBuildFound = false;
            foreach (var build in buildsArray)
            {                
                if ( (int)build["results"] == 0)
                {
                    buildnr = (int)build["number"];
                    isSuccessBuildFound = true;
                    Trace.WriteLine($"build#{buildnr} was built succeessfully.");
                    break;
                }
            }

            if (!isSuccessBuildFound)
            {
                resp = await client.GetAllBuildsOfABuilder(builder);
                JObject buildsObject = ParseJObjectResponse(resp);
                foreach (var buildtoken in buildsObject)
                {
                    var build = buildtoken.Value;                    
                    if ((int)build["results"] == 0)
                    {
                        buildnr = (int)build["number"];
                        isSuccessBuildFound = true;
                        Trace.WriteLine($"build#{buildnr} was built succeessfully.");
                        break;
                    }
                }
            }

            if (!isSuccessBuildFound)
            {
                await client.LaunchBuild(project, builder, branch, revision, "90", null, $"{testcasename}-1");
                Trace.WriteLine($"A build on builder {builder} of project {project} - branch {branch} is launched");

                bool isBuildSuccess = false;
                cnt = 0;
                while (!isBuildSuccess && cnt < 120)
                {                    
                    
                    resp = await client.GetLastXBuilds(builder, 1);
                    JObject build= (JObject)ParseJObjectResponse(resp)["-1"];
                    if (build["results"] != null)
                    {
                        if ((int)build["results"] == 0 && (string)build["reason"] == $"{testcasename}-1")
                        {
                            buildnr = (int)build["number"];
                            isBuildSuccess = true;
                            Trace.WriteLine($"New build#{buildnr} was built succeessfully.");
                            break;
                        }
                    }                    
                    
                    Thread.Sleep(5000);
                    cnt++;
                }
                Assert.True(isBuildSuccess, $"Couldn't make a build on builder {builder} in 10 minutes.");
            }

            resp = await client.Rebuild(project, builder, branch, buildnr.ToString(), $"{testcasename}-rebuild-1");
            resp = await client.Rebuild(project, builder, branch, buildnr.ToString(), $"{testcasename}-rebuild-2");
            resp = await client.Rebuild(project, builder, branch, buildnr.ToString(), $"{testcasename}-rebuild-3");
            Trace.WriteLine($"Rebuild build#{buildnr}..");

            Thread.Sleep(10000);

            //// Step : Check the 2nd and 3rd are not anywhare.  
            bool isDuplicatedRebuild = false;

            resp = await client.GetPendingBuilds(builder);
            JArray pendingBuilds = ParseJArrayResponse(resp);
            if (pendingBuilds.Count > 0)
            {
                foreach (var build in pendingBuilds)
                {
                    if ((string)build["reason"] == $"{testcasename}-rebuild-2" ||
                        (string)build["reason"] == $"{testcasename}-rebuild-3")
                    {
                        isDuplicatedRebuild = true;
                    }
                }
            }

            resp = await client.GetLastXBuilds(builder, 5);
            JObject lastFiveBuilds = ParseJObjectResponse(resp);
            foreach (var kvp in lastFiveBuilds)
            {
                var build = kvp.Value;
                if ((string)build["reason"] == $"{testcasename}-rebuild-2" ||
                    (string)build["reason"] == $"{testcasename}-rebuild-3")
                {
                    isDuplicatedRebuild = true;
                }
            }
        
            //// Step : Wait the rebuild finish.
            cnt = 0;
            bool isFirstBuildSuccess = false;                      
            while (cnt < 240)
            {                
                resp = await client.GetLastXBuilds(builder, 3);
                JObject builds = ParseJObjectResponse(resp);
                foreach (var kvp in builds)
                {
                    var build = kvp.Value;
                    if ((int)build["results"] == 0 && ((string)build["reason"]).Contains($"{testcasename}-rebuild-1"))
                    {
                        isFirstBuildSuccess = true;
                        Trace.WriteLine($"New build#{buildnr} was rebuilt succeessfully.");                        
                    }
                    Trace.WriteLine($"build has result {build["results"].ToString()} ");
                }

                if (isFirstBuildSuccess)
                {
                    break;
                }

                Thread.Sleep(5000);
                cnt++;
            }

            isFirstBuildSuccess.Invoking(t => t.Should().BeTrue("Builds are not completed in 20 minutes"))
                                .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                                .AddResult(errmsgs);

            isDuplicatedRebuild.Invoking(t => t.Should().BeFalse("No duplicated rebuild is launched"))
                                .IgnoreAnyExceptions<Xunit.Sdk.XunitException>()
                                .AddResult(errmsgs);

            #endregion

            #region cleanup and assertion
            await client.StopAllBuildOnBuilder(project, builder, branch);
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
