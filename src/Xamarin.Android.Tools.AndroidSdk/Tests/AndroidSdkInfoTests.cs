﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class AndroidSdkInfoTests
	{
		[Test]
		public void Constructor_NullLogger ()
		{
			Action<TraceLevel, string> logger = null;
			Assert.Throws<ArgumentNullException> (() => new AndroidSdkInfo (logger));
		}

		[Test]
		public void Constructor_Paths ()
		{
			var root    = Path.GetTempFileName ();
			File.Delete (root);
			Directory.CreateDirectory (root);

			var sdk     = Path.Combine (root, "sdk");
			var jdk     = Path.Combine (root, "jdk");

			Directory.CreateDirectory (sdk);
			Directory.CreateDirectory (jdk);

			CreateFauxAndroidSdkDirectory (sdk, "26.0.0");
			CreateFauxJavaSdkDirectory (jdk, "1.8.0", out var _, out var _);

			var logs    = new StringWriter ();
			Action<TraceLevel, string> logger = (level, message) => {
				logs.WriteLine ($"[{level}] {message}");
			};
			var info    = new AndroidSdkInfo (logger, androidSdkPath: sdk, javaSdkPath: jdk, androidNdkPath: null);

			Assert.AreEqual (sdk, info.AndroidSdkPath,  "AndroidSdkPath not preserved!");
			Assert.AreEqual (jdk, info.JavaSdkPath,     "JavaSdkPath not preserved!");
		}

		static  bool    IsWindows   => OS.IsWindows;

		static void CreateFauxAndroidSdkDirectory (string androidSdkDirectory, string buildToolsVersion, ApiInfo [] apiLevels = null)
		{
			var androidSdkToolsPath             = Path.Combine (androidSdkDirectory, "tools");
			var androidSdkBinPath               = Path.Combine (androidSdkToolsPath, "bin");
			var androidSdkPlatformToolsPath     = Path.Combine (androidSdkDirectory, "platform-tools");
			var androidSdkPlatformsPath         = Path.Combine (androidSdkDirectory, "platforms");
			var androidSdkBuildToolsPath        = Path.Combine (androidSdkDirectory, "build-tools", buildToolsVersion);

			Directory.CreateDirectory (androidSdkDirectory);
			Directory.CreateDirectory (androidSdkToolsPath);
			Directory.CreateDirectory (androidSdkBinPath);
			Directory.CreateDirectory (androidSdkPlatformToolsPath);
			Directory.CreateDirectory (androidSdkPlatformsPath);
			Directory.CreateDirectory (androidSdkBuildToolsPath);

			File.WriteAllText (Path.Combine (androidSdkPlatformToolsPath,   IsWindows ? "adb.exe" : "adb"),             "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath,      IsWindows ? "zipalign.exe" : "zipalign"),   "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath,      IsWindows ? "aapt.exe" : "aapt"),           "");
			File.WriteAllText (Path.Combine (androidSdkToolsPath,           IsWindows ? "lint.bat" : "lint"),           "");

			List<ApiInfo> defaults = new List<ApiInfo> ();
			for (int i = 10; i < 26; i++) {
				defaults.Add (new ApiInfo () {
					Id = i.ToString (),
				});
			}
			foreach (var level in ((IEnumerable<ApiInfo>) apiLevels) ?? defaults) {
				var dir = Path.Combine (androidSdkPlatformsPath, $"android-{level.Id}");
				Directory.CreateDirectory(dir);
				File.WriteAllText (Path.Combine (dir, "android.jar"), "");
			}
		}

		struct ApiInfo {
			public  string      Id;
		}

		protected string CreateFauxJavaSdkDirectory (string javaPath, string javaVersion, out string javaExe, out string javacExe)
		{
			javaExe             = IsWindows ? "Java.cmd" : "java.bash";
			javacExe            = IsWindows ? "Javac.cmd" : "javac.bash";
			var jarSigner       = IsWindows ? "jarsigner.exe" : "jarsigner";
			var javaBinPath     = Path.Combine (javaPath, "bin");

			Directory.CreateDirectory (javaBinPath);

			CreateFauxJavaExe (Path.Combine (javaBinPath, javaExe), javaVersion);
			CreateFauxJavacExe (Path.Combine (javaBinPath, javacExe), javaVersion);

			File.WriteAllText (Path.Combine (javaBinPath, jarSigner), "");
			return javaPath;
		}

		void CreateFauxJavaExe (string javaExeFullPath, string version)
		{
			var sb  = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo java version \"{version}\"");
				sb.AppendLine ($"echo Java(TM) SE Runtime Environment (build {version}-b13)");
				sb.AppendLine ($"echo Java HotSpot(TM) 64-Bit Server VM (build 25.101-b13, mixed mode)");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"java version \\\"{version}\\\"\"");
				sb.AppendLine ($"echo \"Java(TM) SE Runtime Environment (build {version}-b13)\"");
				sb.AppendLine ($"echo \"Java HotSpot(TM) 64-Bit Server VM (build 25.101-b13, mixed mode)\"");
			}
			File.WriteAllText (javaExeFullPath, sb.ToString ());
			if (!IsWindows) {
				Exec ("chmod", $"u+x \"{javaExeFullPath}\"");
			}
		}

		void CreateFauxJavacExe (string javacExeFullPath, string version)
		{
			var sb  = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo javac {version}");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"javac {version}\"");
			}
			File.WriteAllText (javacExeFullPath, sb.ToString ());
			if (!IsWindows) {
				Exec ("chmod", $"u+x \"{javacExeFullPath}\"");
			}
		}

		protected void Exec (string exe, string args)
		{
			var psi     = new ProcessStartInfo {
				FileName                = exe,
				Arguments               = args,
				UseShellExecute         = false,
				RedirectStandardInput   = false,
				RedirectStandardOutput  = false,
				RedirectStandardError   = false,
				CreateNoWindow          = true,
				WindowStyle             = ProcessWindowStyle.Hidden,

			};
			var proc    = Process.Start (psi);
			if (!proc.WaitForExit ((int) TimeSpan.FromSeconds(30).TotalMilliseconds)) {
				proc.Kill ();
				proc.WaitForExit ();
			}
		}
	}
}
