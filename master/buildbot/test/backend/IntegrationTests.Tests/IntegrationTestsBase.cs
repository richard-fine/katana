using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Unity.Katana.IntegrationTests.Client;
using Xunit;

namespace Unity.Katana.IntegrationTests.Tests
{
    public class IntegrationTestsBase
    {
        protected string settingfile = "test.json";

        protected TextWriterTraceListener SetupTraceFile(string filename)
        {
            TextWriterTraceListener myTextListener = null;
            string datetime = DateTime.UtcNow.ToUniversalTime().ToString("ddMMyyyyHHmmss");            
            Stream myFileName = File.Create(filename.Replace(".log", $"{datetime}.log"));
            StreamWriter myOutputWriter = new StreamWriter(myFileName);
            myTextListener = new TextWriterTraceListener(myOutputWriter);
            Trace.Listeners.Add(myTextListener);
            return myTextListener;
        }

        protected void WriteTraceToFile(TextWriterTraceListener myTextListener)
        {
            myTextListener.Flush();
            Trace.Listeners.Remove(myTextListener);
            myTextListener.Dispose();
        }


        protected List<int> WaitAllBuildsAreRunning(List<string> revision,
                                           KatanaClient client,
                                           string builder,
                                           Action method = null,
                                           int X = 3,
                                           int num = 10)
        {
            bool isAllRunning = false;
            int counter = 0;            
            List<int> builds = Enumerable.Repeat(-1, revision.Count).ToList();


            //// Wait until all the test build is running ////
            while (!isAllRunning)
            {
                counter++;
                if (method != null) {
                    method();
                }
                if (counter > 60)
                    new TimeoutException("Testcase failed after running for 5 minutes");

                //// Read the build number ////                
                if (revision.Count > 1)
                {
                    for (int i = 0; i < builds.Count; i++)
                    {
                        builds[i] = client.GetBuildNumberFromRevision(revision[i], builder);
                    }
                }
                else
                {                    
                    builds = client.GetXBuildNumberFromRevision(revision[0], builder, X, num);
                }
                
                isAllRunning = builds.All(x => x >= 0);
                if (!isAllRunning)
                {
                    Thread.Sleep(5000);
                }                
            }
            return builds;
        }

        /// <summary>
        /// Assert the testcase by check the the number of element in errmsgs list 
        /// </summary>
        /// <param name="errmsgs"></param>
        public void AssertTestcase(List<string> errmsgs)
        {
            Assert.True(errmsgs.Count == 0,
                        string.Format("message: {0}", string.Join("; \r\n", errmsgs.Select(x => x).ToArray())));
        }
    }
}
