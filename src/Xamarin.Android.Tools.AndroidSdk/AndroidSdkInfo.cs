using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools
{
	public class AndroidSdkInfo
	{
		AndroidSdkBase sdk;

		public AndroidSdkInfo (Action<TraceLevel, string> logger = null, string androidSdkPath = null, string androidNdkPath = null, string javaSdkPath = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			sdk = CreateSdk (logger);
			sdk.Initialize (androidSdkPath, androidNdkPath, javaSdkPath);

			// shouldn't happen, in that sdk.Initialize() should throw instead
			if (string.IsNullOrEmpty (AndroidSdkPath))
				throw new InvalidOperationException ($"Could not determine Android SDK location. Please provide `{nameof (androidSdkPath)}`.");
			if (string.IsNullOrEmpty (JavaSdkPath))
				throw new InvalidOperationException ($"Could not determine Java SDK location. Please provide `{nameof (javaSdkPath)}`.");
		}

		static AndroidSdkBase CreateSdk (Action<TraceLevel, string> logger)
		{
			return OS.IsWindows
				? (AndroidSdkBase) new AndroidSdkWindows (logger)
				: (AndroidSdkBase) new AndroidSdkUnix (logger);
		}

		public IEnumerable<string> GetBuildToolsPaths (string preferredBuildToolsVersion)
		{
			if (!string.IsNullOrEmpty (preferredBuildToolsVersion)) {
				var preferredDir = Path.Combine (AndroidSdkPath, "build-tools", preferredBuildToolsVersion);
				if (Directory.Exists (preferredDir))
					return new[] { preferredDir }.Concat (GetBuildToolsPaths ().Where (p => p!= preferredDir));
			}
			return GetBuildToolsPaths ();
		}

		public IEnumerable<string> GetBuildToolsPaths ()
		{
			var buildTools  = Path.Combine (AndroidSdkPath, "build-tools");
			if (Directory.Exists (buildTools)) {
				var preview = Directory.EnumerateDirectories (buildTools)
					.Where(x => sdk.TryParseVersion (Path.GetFileName (x)) == null)
					.Select(x => x);

				foreach (var d in preview)
					yield return d;

				foreach (var d in sdk.GetSortedToolDirectoryPaths (buildTools))
					yield return d;
			}
			var ptPath  = Path.Combine (AndroidSdkPath, "platform-tools");
			if (Directory.Exists (ptPath))
				yield return ptPath;
		}

		public IEnumerable<AndroidVersion> GetInstalledPlatformVersions (AndroidVersions versions)
		{
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));
			return versions.InstalledBindingVersions
				.Where (p => TryGetPlatformDirectoryFromApiLevel (p.Id, versions) != null) ;
		}

		public string GetPlatformDirectory (int apiLevel)
		{
			return GetPlatformDirectoryFromId (apiLevel.ToString ());
		}

		public string GetPlatformDirectoryFromId (string id)
		{
			return Path.Combine (AndroidSdkPath, "platforms", "android-" + id);
		}

		public string TryGetPlatformDirectoryFromApiLevel (string idOrApiLevel, AndroidVersions versions)
		{
			var id  = versions.GetIdFromApiLevel (idOrApiLevel);
			var dir = GetPlatformDirectoryFromId (id);

			if (Directory.Exists (dir))
				return dir;

			var level   = versions.GetApiLevelFromId (id);
			dir         = level.HasValue ? GetPlatformDirectory (level.Value) : null;
			if (dir != null && Directory.Exists (dir))
				return dir;

			return null;
		}

		public bool IsPlatformInstalled (int apiLevel)
		{
			return apiLevel != 0 && Directory.Exists (GetPlatformDirectory (apiLevel));
		}

		public string AndroidNdkPath {
			get { return sdk.AndroidNdkPath; }
		}

		public string AndroidSdkPath {
			get { return sdk.AndroidSdkPath; }
		}

		public string [] AllAndroidSdkPaths {
			get {
				return sdk.AllAndroidSdks ?? new string [0];
			}
		}

		public string JavaSdkPath {
			get { return sdk.JavaSdkPath; }
		}

		public string AndroidNdkHostPlatform {
			get { return sdk.NdkHostPlatform; }
		}

		public static void SetPreferredAndroidNdkPath (string path, Action<TraceLevel, string> logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredAndroidNdkPath(path);
		}

		internal static void DefaultConsoleLogger (TraceLevel level, string message)
		{
			switch (level) {
			case TraceLevel.Error:
				Console.Error.WriteLine (message);
				break;
			default:
				Console.WriteLine ($"[{level}] {message}");
				break;
			}
		}

		public static void SetPreferredAndroidSdkPath (string path, Action<TraceLevel, string> logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredAndroidSdkPath (path);
		}

		public static void SetPreferredJavaSdkPath (string path, Action<TraceLevel, string> logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredJavaSdkPath (path);
		}

		public static void DetectAndSetPreferredJavaSdkPathToLatest (Action<TraceLevel, string> logger = null)
		{
			if (OS.IsWindows)
				throw new NotImplementedException ("Windows is not supported at this time.");

			logger          = logger ?? DefaultConsoleLogger;

			var latestJdk   = JdkInfo.GetMacOSMicrosoftJdks (logger).FirstOrDefault ();
			if (latestJdk == null)
				throw new NotSupportedException ("No Microsoft OpenJDK could be found.  Please re-run the Visual Studio installer or manually specify the JDK path in settings.");

			var sdk = CreateSdk (logger);
			sdk.SetPreferredJavaSdkPath (latestJdk.HomePath);
		}
	}
}
