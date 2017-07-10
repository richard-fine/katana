using System;
using System.Collections.Generic;
using System.Text;
using Unity.Katana.IntegrationTests.Client;

namespace Unity.Katana.IntegrationTests.Tests
{
    public static class PropertyDataSource
    {
        private static KatanaBuilder UnityMacEditorTrunk = new KatanaBuilder(
            "Unity",
            "proj0-Build MacEditor",
            "trunk"
            );

        private static KatanaBuilder UnityTestDeploymentTestsServiceTizenTrunk = new KatanaBuilder(
            "Unity",
            "proj0-Test DeploymentTests - Services - Tizen",
            "trunk"
            );


        private static KatanaBuilder FMODLinuxArmTrunk= new KatanaBuilder(
            "FMOD",
            "proj2-Linux ARM",
            "trunk"
            );
        private static KatanaBuilder FMODAndroidTrunk = new KatanaBuilder(
            "FMOD",
            "proj2-Android",
            "trunk"
            );

        private static KatanaBuilder ATICompressCompleteDefault = new KatanaBuilder(
            "ATI%20Compress",
            "proj39-Build%20ATICompress%20Complete",
            "default"
            );


        public static IEnumerable<object[]> Data_StopAllRunningBuildReliabilityTest { get; } = new List<object[]>
            {
                new object[] {
                    "Unity",
                    "proj0-Build MacEditor",
                    "trunk",
                    new string[]
                    {
                        "24c95392f5c2",
                        "9683b9f88e0e",
                        "43294e7c9854",
                        "8d4e8eefeb52",
                        "32e0dff84ceb"
                    }
                }

            };

        public static IEnumerable<object[]> Data_StopBuildsOnMultipleBuilder { get; } = new List<object[]>
            {
                new object[] {
                    new List<KatanaBuild> {
                        new KatanaBuild(UnityMacEditorTrunk, "24c95392f5c2"),
                        new KatanaBuild(UnityMacEditorTrunk, "9683b9f88e0e"),
                        new KatanaBuild(UnityTestDeploymentTestsServiceTizenTrunk, "8d4e8eefeb52"),
                        new KatanaBuild(FMODAndroidTrunk, "85c3c6e06468")
                    }
                    
                }
            };
    }
}



