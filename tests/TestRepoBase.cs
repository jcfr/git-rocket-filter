﻿// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using Xunit;
using Xunit.Abstractions;

namespace GitRocketFilter.Tests
{
    public abstract class TestRepoBase
    {
        protected const string NewBranch = "new_master";
        protected const string NewBranchRef = "refs/heads/new_master";
        private readonly ITestOutputHelper outputHelper;

        protected TestRepoBase(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        protected DisposeTempRepo InitializeTest([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            var repoName = GetType().Name + "_" + memberName;
            var repoPath = Path.Combine(path, repoName);
            if (Directory.Exists(repoPath))
            {
                RemoveDirectory(repoPath);
            }
            Directory.CreateDirectory(repoPath);
            var repoSourcePath = Path.Combine(path, @"..\..\..\test_repo\");
            DirectoryCopy(repoSourcePath, repoPath, true);
            Directory.Move(Path.Combine(repoName, "dotgit"), Path.Combine(repoName, ".git"));

            var tempRepo = new DisposeTempRepo(repoPath);
            Program.RedirectOutput = new TextWriterRedirect(outputHelper, tempRepo.OutputBuilder);
            return tempRepo;
        }

        protected static string AssertBranchRef(Repository repo)
        {
            Assert.NotNull(repo.Refs[NewBranchRef]);
            return repo.Refs[NewBranchRef].TargetIdentifier;
        }

        protected static List<Commit> GetCommits(Repository repo, string since = null)
        {
            var filter = new CommitFilter() {SortBy = CommitSortStrategies.Topological};
            if (since != null)
            {
                filter.IncludeReachableFrom = since;
            }
            return
                repo.Commits.QueryBy(filter)
                    .ToList();
        }
        
        protected static List<Commit> GetCommitsFromRange(Repository repo, string range)
        {
            return
                repo.Commits.QueryBy(new CommitFilter() {Range = range, SortBy = CommitSortStrategies.Topological})
                    .ToList();
        }

        // http://stackoverflow.com/a/648055/1356325
        private static void RemoveDirectory(string directoryPath)
        {
            if (directoryPath == null) throw new ArgumentNullException("directoryPath");
            RemoveDirectory(new DirectoryInfo(directoryPath));
        }

        private static void RemoveDirectory(FileSystemInfo fileSystemInfo)
        {
            var directoryInfo = fileSystemInfo as DirectoryInfo;
            if (directoryInfo != null)
            {
                foreach (var childInfo in directoryInfo.GetFileSystemInfos())
                {
                    RemoveDirectory(childInfo);
                }
            }

            fileSystemInfo.Attributes = FileAttributes.Normal;
            fileSystemInfo.Delete();
        }

        // https://msdn.microsoft.com/en-us/library/bb762914%28v=vs.110%29.aspx
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        protected static IEnumerable<TreeEntry> GetEntries(Tree tree)
        {
            foreach (var entry in tree)
            {
                if (entry.TargetType == TreeEntryTargetType.Tree)
                {
                    var subTree = (Tree)entry.Target;
                    foreach (var subEntry in GetEntries(subTree))
                    {
                        yield return subEntry;
                    }
                }
                else if (entry.TargetType == TreeEntryTargetType.Blob)
                {
                    yield return entry;
                }
            }
        }

        protected struct DisposeTempRepo : IDisposable
        {
            private string text;

            public DisposeTempRepo(string path)
            {
                Path = path;
                Repo = new Repository(path);
                text = null;
                OutputBuilder = new StringBuilder();
            }

            public readonly string Path;

            public readonly Repository Repo;

            public String Output
            {
                get { return text ?? (text = OutputBuilder.ToString()); }
            }

            internal readonly StringBuilder OutputBuilder;

            public void Dispose()
            {
                Repo.Dispose();
                RemoveDirectory(Path);
            }
        }

        private class TextWriterRedirect : TextWriter
        {
            private readonly ITestOutputHelper outputHelper;
            private readonly StringBuilder globalLogger;
            private readonly StringBuilder buffer;

            public TextWriterRedirect(ITestOutputHelper outputHelper, StringBuilder globalLogger)
            {
                this.outputHelper = outputHelper;
                this.globalLogger = globalLogger;
                buffer = new StringBuilder(1024);
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    var text = buffer.ToString();
                    outputHelper.WriteLine(text);
                    globalLogger.AppendLine(text);
                    buffer.Clear();
                }
                else
                {
                    buffer.Append(value);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (buffer.Length > 0)
                {
                    var text = buffer.ToString();
                    outputHelper.WriteLine(text);
                    globalLogger.AppendLine(text);
                    buffer.Clear();
                }
                base.Dispose(disposing);
            }
        }
    }
}
