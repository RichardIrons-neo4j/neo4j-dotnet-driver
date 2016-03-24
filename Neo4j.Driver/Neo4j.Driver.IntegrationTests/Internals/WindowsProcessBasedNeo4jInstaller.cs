﻿//  Copyright (c) 2002-2016 "Neo Technology,"
//  Network Engine for Objects in Lund AB [http://neotechnology.com]
// 
//  This file is part of Neo4j.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Neo4j.Driver.IntegrationTests.Internals
{
    /// <summary>
    ///     A process based Neo4j installer that uses a background process for
    ///     running the Neo4j server.
    /// </summary>
    public class WindowsProcessBasedNeo4jInstaller : INeo4jInstaller
    {
        private const string eventSourceName = "Neo4jTests";

        private readonly int delayAfterStartingServer;

        private Process startedProcess;

        public WindowsProcessBasedNeo4jInstaller(int delayAfterStartingServer = 15000)
        {
            this.delayAfterStartingServer = delayAfterStartingServer;
        }

        public DirectoryInfo Neo4jHome { get; private set; }

        public void DownloadNeo4j()
        {
            Neo4jHome = Neo4jServerFilesDownloadHelper.DownloadNeo4j(
                Neo4jServerEdition.Enterprise,
                Neo4jServerPlatform.Windows,
                new ZipNeo4jServerFileExtractor());

            UpdateSettings(new Dictionary<string, string> {{"dbms.security.auth_enabled", "false"}}); // disable auth
        }

        public void InstallServer()
        {
            // Not needed
        }

        public void StartServer()
        {
            try
            {
                var processInfo = new ProcessStartInfo();
                processInfo.FileName = Path.Combine(Neo4jHome.FullName, @"bin\neo4j.bat");
                processInfo.WorkingDirectory = Neo4jHome.FullName;
                processInfo.Arguments = "console";
                processInfo.UseShellExecute = true;
                processInfo.CreateNoWindow = false;

                EventLog.WriteEntry(eventSourceName,
                    $"Starting process: {processInfo.FileName} with working directory: {processInfo.WorkingDirectory} and arguments: {processInfo.Arguments}",
                    EventLogEntryType.Information);

                startedProcess = Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(eventSourceName, ex.ToString(), EventLogEntryType.Error);
                throw;
            }

            Task.Delay(delayAfterStartingServer).Wait();
        }

        public void StopServer()
        {
            if (startedProcess == null) return;

            try
            {
                EventLog.WriteEntry(eventSourceName, "Stopping process");
                startedProcess.CloseMainWindow();
                if (!startedProcess.WaitForExit(10000))
                {
                    startedProcess.Kill();
                    startedProcess.WaitForExit();
                }
                startedProcess = null;
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(eventSourceName, ex.ToString(), EventLogEntryType.Error);
                throw;
            }
        }

        public void UninstallServer()
        {
            // Not needed
        }

        public void UpdateSettings(IDictionary<string, string> keyValuePair)
        {
            Neo4jSettingsHelper.UpdateSettings(Neo4jHome.FullName, keyValuePair);
        }
    }
}