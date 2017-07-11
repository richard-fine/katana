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
using Xunit.Abstractions;
using Unity.Katana.IntegrationTests.Client;
using Unity.Katana.IntegrationTests.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FluentAssertions;

namespace Unity.Katana.IntegrationTests.Tests
{
    public class ReliabilityTest : IntegrationTestsBase
    {
        private readonly ITestOutputHelper output;
        public ReliabilityTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Start 5 builds and stop them. Repeat the test. 
        /// No builds should be stuck on slave
        /// </summary>
        /// <returns></returns>
        /// <remarks>There are async method not awaited, which is on purpose. Hope the behaviour can help 
        ///          trigger error </remarks>
        [Theory]
        [MemberData("Data_StopBuildWitRevisionReliabilityTest", MemberType = typeof(PropertyDataSource))]
        public async Task StopBuildWitRevisionReliabilityTest(KatanaBuilder katanabuilder, List<string> revision_list)
        {
            #region arrange
            string testcasename = GetTestcaseName();
            TextWriterTraceListener myTextListener = SetupTraceFile($"{testcasename}.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            string _baseAddress = settings["BaseAddress"].ToString();
            client.SetBaseAddress(_baseAddress);
            Trace.WriteLine($"Set base address {_baseAddress}");            
            var project = katanabuilder.Project;
            var builder = katanabuilder.Builder;
            var branch = katanabuilder.Branch;            
            Assert.True(revision_list.Count >= 5, "Test need at least 5 revision string");
            Trace.WriteLine($"Read parameter project : {project}, builder: {builder}, branch: {branch}");
            bool isWaiting = false;
            int length = 20;
            #endregion

            #region action
            //// launch 5 setup builds to make the slave busy  ////
            for (int i = 0; i < length; i++)
            {
                await client.LaunchBuild(project, builder, branch, revision_list[0], "99");
                await client.LaunchBuild(project, builder, branch, revision_list[1], "99");
                await client.LaunchBuild(project, builder, branch, revision_list[2], "10");
                await client.LaunchBuild(project, builder, branch, revision_list[3], "90");
                await client.LaunchBuild(project, builder, branch, revision_list[4], "40");
                Thread.Sleep(10000);
                Trace.WriteLine("5 builds are launched");

                bool isBuild1Stopped = false;
                bool isBuild2Stopped = false;
                bool isBuild3Stopped = false;
                bool isBuild4Stopped = false;
                bool isBuild5Stopped = false;

                int counter = 0;
                int build1 = -1;
                int build2 = -1;
                int build3 = -1;
                int build4 = -1;
                int build5 = -1;

                //// Wait until all the test build is running ////
                while (!isBuild1Stopped || !isBuild2Stopped || !isBuild3Stopped || !isBuild4Stopped || !isBuild5Stopped)
                {
                    counter++;
                    if (counter > 60)
                        Assert.True(false, "Testcase failed after running for 2 minutes");

                    //// Stop the setup builds if they are running ////
                    build1 = client.GetBuildNumberFromRevisionAndTime(revision_list[0], builder, 0, 40, 5);
                    build2 = client.GetBuildNumberFromRevisionAndTime(revision_list[1], builder, 0, 40, 5);
                    build3 = client.GetBuildNumberFromRevisionAndTime(revision_list[2], builder, 0, 40, 5);
                    build4 = client.GetBuildNumberFromRevisionAndTime(revision_list[3], builder, 0, 40, 5);
                    build5 = client.GetBuildNumberFromRevisionAndTime(revision_list[4], builder, 0, 40, 5);

                    if (build1 >= 0)
                    {
                        Func<Task> func =
                            async () => await client.StopBuild(project, builder, build1.ToString(), branch, isWaiting);

                        RunActionForXTimeAsync(func, 5);
                        Trace.WriteLine($"Stopping build1 #{build1}");
                        //Thread.Sleep(1000);
                        isBuild1Stopped = true;
                    }
                    if (build2 >= 0)
                    {
                        Func<Task> func =
                            async () => await client.StopBuild(project, builder, build2.ToString(), branch, isWaiting);

                        RunActionForXTimeAsync(func, 5);
                        Trace.WriteLine($"Stopping build2 #{build2}");
                        isBuild2Stopped = true;
                    }

                    if (build3 >= 0)
                    {
                        Func<Task> func =
                            async () => await client.StopBuild(project, builder, build3.ToString(), branch, isWaiting);

                        RunActionForXTimeAsync(func, 5);
                        Trace.WriteLine($"Stopping build3 #{build3}");
                        isBuild3Stopped = true;

                    }
                    if (build4 >= 0)
                    {
                        Func<Task> func =
                            async () => await client.StopBuild(project, builder, build4.ToString(), branch, isWaiting);

                        RunActionForXTimeAsync(func, 5);
                        Trace.WriteLine($"Stopping build4 #{build4}");
                        isBuild4Stopped = true;
                    }

                    if (build5 >= 0)
                    {
                        Func<Task> func =
                            async () => await client.StopBuild(project, builder, build5.ToString(), branch, isWaiting);

                        RunActionForXTimeAsync(func, 5);
                        Trace.WriteLine($"Stopping build5 #{build5}");
                        isBuild5Stopped = true;
                    }
                    if (!isBuild1Stopped || !isBuild2Stopped ||
                        !isBuild3Stopped || !isBuild4Stopped || !isBuild5Stopped)
                        Trace.WriteLine("not all the builds are stopped,   " +
                            $"build1: {isBuild1Stopped.ToString()}," +
                            $"build2: {isBuild2Stopped.ToString()}, " +
                            $"build3: {isBuild3Stopped.ToString()}, " +
                            $"build4: {isBuild4Stopped.ToString()}, " +
                            $"build5: {isBuild5Stopped.ToString()}" +
                            "refresh after 5 seconds");
                    Thread.Sleep(5000);
                }
                #endregion

                HttpResponseMessage resp = await client.GetCachedBuildsOfABuilder(builder);
                JArray builds = ParseJArrayResponse(resp);
                foreach (var build in builds)
                {
                    //"No builds has null result, meaning they are not running "
                    Assert.NotNull(build["results"]);
                }
                Thread.Sleep(5000);
            }
            #region clean up and assertion            
            WriteTraceToFile(myTextListener);
            #endregion
        }

        [Theory]
        [MemberData("Data_StopAllRunningBuildReliabilityTest", MemberType = typeof(PropertyDataSource))]
        public async Task StopAllRunningBuildReliabilityTest(KatanaBuilder katanabuilder, List<string> revision_list)
        {
            #region arrange
            string testcasename = GetTestcaseName();
            TextWriterTraceListener myTextListener = SetupTraceFile($"{testcasename}.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            string _baseAddress = settings["BaseAddress"].ToString();
            client.SetBaseAddress(_baseAddress);
            Trace.WriteLine($"Set base address {_baseAddress}");
            var project = katanabuilder.Project;
            var builder = katanabuilder.Builder;
            var branch = katanabuilder.Branch;            
            Assert.True(revision_list.Count >= 5, "Test need at least 5 revision string");
            Trace.WriteLine($"Read parameter project : {project}, builder: {builder}, branch: {branch}");
            bool isWaiting = false;
            int length = 20;
            HttpResponseMessage resp = null;
            JObject builds = null;
            //await FreeAllSlavesOfABuilder(client, builder);
            #endregion

            #region action
            //// launch 5 setup builds to make the slave busy  ////
            for (int i = 0; i < length; i++)
            {
                await client.LaunchBuild(project, builder, branch, revision_list[0], "99");
                await client.LaunchBuild(project, builder, branch, revision_list[1], "99");
                await client.LaunchBuild(project, builder, branch, revision_list[2], "10");
                await client.LaunchBuild(project, builder, branch, revision_list[3], "90");
                await client.LaunchBuild(project, builder, branch, revision_list[4], "40");
                Thread.Sleep(10000);
                Trace.WriteLine("5 builds are launched");

                bool isRunningOrPendingBuild = true;
                int counter = 0;
                //// Wait until all the test build is running ////
                while (isRunningOrPendingBuild)
                {
                    counter++;
                    if (counter > 60)
                        Assert.True(false, "Testcase failed after running for 5 minutes");
                    resp = await client.GetAllBuildsOfABuilder(builder);
                    builds = ParseJObjectResponse(resp);
                    bool _isRunningbuild = false;
                    foreach (var kvp in builds)
                    {
                        JObject build = (JObject)kvp.Value;
                        if (string.IsNullOrEmpty(build["results"].ToString()) || (int)build["results"] == 9)
                        {
                            _isRunningbuild = true;
                            if (build["number"] != null)
                            {
                                int buildnr = (int)build["number"];
                                Func<Task> func =
                                            async () =>
                                            await client.StopBuild(project, builder,
                                                                   buildnr.ToString(), branch, isWaiting);
                                Trace.WriteLine($"Stop build#{buildnr}");

                                RunActionForXTimeAsync(func, 5);
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Builds#{build["number"].ToString()} has result : {build["results"]}");
                        }
                    }

                    resp = await client.GetCachedBuildsOfABuilder(builder);
                    JArray buildsArray = ParseJArrayResponse(resp);
                    foreach (var build in buildsArray)
                    {
                        if (string.IsNullOrEmpty(build["results"].ToString()))
                        {
                            _isRunningbuild = true;
                            if (build["number"] != null)
                            {
                                int buildnr = (int)build["number"];
                                Func<Task> func =
                                            async () =>
                                            await client.StopBuild(project, builder,
                                                                   buildnr.ToString(), branch, isWaiting);
                                Trace.WriteLine($"Stop build#{buildnr}");

                                RunActionForXTimeAsync(func, 5);
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Builds#{build["number"].ToString()} has result : {build["results"]}");
                        }
                    }

                    bool _isPendingbuild = false;
                    resp = await client.GetPendingBuilds(builder);
                    JArray pendingBuilds = ParseJArrayResponse(resp);
                    if (pendingBuilds.Count > 0)
                    {
                        _isPendingbuild = true;
                        Trace.WriteLine($"{pendingBuilds.Count} builds are pending.");
                    }

                    isRunningOrPendingBuild = _isRunningbuild || _isPendingbuild;
                    Trace.WriteLine($"Running build: {_isRunningbuild.ToString()}, Pending build: {_isPendingbuild.ToString()}");
                    Thread.Sleep(5000);
                }

            }
            #endregion
            #region clean up and assertion            
            resp = await client.GetAllBuildsOfABuilder(builder);
            builds = ParseJObjectResponse(resp);
            foreach (var kvp in builds)
            {
                //"No builds has null result, meaning they are not running "
                JObject build = (JObject)kvp.Value;
                Trace.WriteLine($"Result is {build["results"]}");
                Assert.NotNull(build["results"]);
            }            
            WriteTraceToFile(myTextListener);
            //await client.StopAllBuildOnBuilder(project, builder, branch);            
            #endregion
        }


        /// <summary>
        /// Launch builds on muiltple builder, and stop them!
        /// </summary>
        /// <returns></returns>
        [Theory]
        [MemberData("Data_StopBuildsOnMultipleBuilder", MemberType = typeof(PropertyDataSource))]        
        public async Task StopBuildsOnMultipleBuilder(List<KatanaBuild> katanabuilds)
        {
            #region arrange
            string testcasename = GetTestcaseName();
            TextWriterTraceListener myTextListener = SetupTraceFile($"{testcasename}.log");
            var client = new KatanaClient();

            JObject settings = JObject.Parse(File.ReadAllText(settingfile));
            string _baseAddress = settings["BaseAddress"].ToString();
            client.SetBaseAddress(_baseAddress);
            Trace.WriteLine($"Set base address {_baseAddress}");
                                    
            bool isWaiting = false;
            int length = 20;
            #endregion

            for (int i = 0; i < length; i++)
            {
                Trace.WriteLine($"Iteration # {i}");
                foreach (var katanabuild in katanabuilds)
                {
                    await FreeAllSlavesOfABuilder(client, katanabuild.Builder.Builder);
                    await client.LaunchBuild(katanabuild);
                    Trace.WriteLine($"Read parameter project : {katanabuild.Builder.Project}, " +
                                                   $"builder: {katanabuild.Builder.Builder}, " +
                                                   $"branch: {katanabuild.Builder.Branch}" +
                                                   $"revision: {katanabuild.Revision}");

                    Thread.Sleep(1000);
                }

                int counter = 0;


                while (katanabuilds.Any(s => !s.Stopping))
                {
                    counter++;
                    if (counter > 60)
                        Assert.True(false, "Testcase failed after running for 2 minutes");

                    //// Stop the setup builds if they are running ////
                    foreach (var katanabuild in katanabuilds)
                    {
                        katanabuild.Build =
                            client.GetBuildNumberFromRevisionAndTime(katanabuild.Revision,
                                                                    katanabuild.Builder.Builder, 0, 50, 3);
                        if (katanabuild.Build >= 0)
                        {
                            Func<Task> func = async () => await client.StopBuild(katanabuild, isWaiting);
                            await RunActionForXTimeAsync(func, 5);
                            Trace.WriteLine($"Stopping build #{katanabuild.Build}");
                            katanabuild.Starting = false;
                            katanabuild.Running = false;
                            katanabuild.Stopped = false;
                            katanabuild.Stopping = true;
                        }
                    }

                    if (katanabuilds.Any(s => !s.Stopping))
                    {
                        Trace.WriteLine("not all the builds are stopped, refresh after 5 seconds : ");
                        foreach (var katanabuild in katanabuilds)
                        {
                            Trace.WriteLine($"Build#{katanabuild.Build} : {katanabuild.Stopping.ToString()}");
                        }
                    }                    
                }

                foreach (var katanabuild in katanabuilds)
                {
                    HttpResponseMessage resp = await client.GetCachedBuildsOfABuilder(katanabuild.Builder.Builder);
                    JArray builds_array = ParseJArrayResponse(resp);
                    foreach (var build in builds_array)
                    {
                        //"No builds has null result, meaning they are not running "
                        Assert.NotNull(build["results"]);
                    }
                }
                Thread.Sleep(5000);
            }
            
            #region clean up and assertion            
            WriteTraceToFile(myTextListener);
            #endregion
        }

        [Fact]
        [Trait("Status", "Unstable")]
        public async Task ReprocudeRacingErrorTest()
        {
            #region arrange
            string testcasename = GetTestcaseName();
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
            JArray revisions = (JArray)setup["revisions"];
            List<string> revision_list = new List<string>();
            foreach (var rev in revisions)
            {
                revision_list.Add(rev.ToString());
            }
            Assert.True(revision_list.Count >= 5, "Test need at least 5 revision string");
            Trace.WriteLine($"Read parameter project : {project}, builder: {builder}, branch: {branch}");
            //await FreeAllSlavesOfABuilder(client, builder);
            #endregion

            #region action
            //// launch 2 setup builds to make the slave busy  ////
            await client.LaunchBuild(project, builder, branch, revision_list[0], "99");
            await client.LaunchBuild(project, builder, branch, revision_list[1], "99");
            Trace.WriteLine("Two setup builds are launched");
            Thread.Sleep(3000);
            //// launch 3 test builds. ////
            await client.LaunchBuild(project, builder, branch, revision_list[2], "10");
            await client.LaunchBuild(project, builder, branch, revision_list[3], "90");
            await client.LaunchBuild(project, builder, branch, revision_list[4], "40");
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
                build1 = client.GetBuildNumberFromRevision(revision_list[0], builder);
                build2 = client.GetBuildNumberFromRevision(revision_list[1], builder);
                if (build1 >= 0)
                {
                    client.StopBuild(project, builder, build1.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build1}");
                    //Thread.Sleep(1000);
                    isBuild1Stopped = true;
                }
                if (build2 >= 0)
                {
                    client.StopBuild(project, builder, build2.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build2}");
                    //Thread.Sleep(1000);
                    isBuild2Stopped = true;
                }

                Thread.Sleep(2000);
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
                int _build1 = client.GetBuildNumberFromRevision(revision_list[2], builder);
                int _build2 = client.GetBuildNumberFromRevision(revision_list[3], builder);
                int _build3 = client.GetBuildNumberFromRevision(revision_list[4], builder);

                if (_build1 >= 0)
                {
                    build1 = _build1;
                    client.StopBuild(project, builder, build1.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build1}");
                    //Thread.Sleep(1000);
                    isBuild1Stopped = true;

                }
                if (_build2 >= 0)
                {
                    build2 = _build2;
                    client.StopBuild(project, builder, build2.ToString(), branch);
                    Trace.WriteLine($"Stopping build {build2}");
                    //Thread.Sleep(1000);
                    isBuild2Stopped = true;
                }

                if (_build3 >= 0)
                {
                    build3 = _build3;
                    client.StopBuild(project, builder, build3.ToString(), branch);
                    Console.WriteLine($"Stopping build {build3}");
                    //Thread.Sleep(1000);
                    isBuild3Stopped = true;
                }

                Trace.WriteLine($"builds number : {build1}, {build2}, {build3}");
                if (!isBuild1Stopped || !isBuild2Stopped || !isBuild3Stopped)
                    Trace.WriteLine("not all the builds are running,  refresh after 5 seconds");
                Thread.Sleep(2000);
            }
            #endregion

            #region clean up and assertion            
            WriteTraceToFile(myTextListener);
            //await client.StopAllBuildOnBuilder(project, builder, branch);            
            #endregion
        }
    }
}
