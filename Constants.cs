using System;

namespace ClipboardAccelerator
{
	class Constants
	{
		public static readonly string APPLICATION_NAME = "Clipboard Accelerator";
		public static readonly string APPLICATION_DATA_PATH = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\{APPLICATION_NAME}\\";
		public static readonly string APPLICATION_TOOLS_PATH = $"{APPLICATION_DATA_PATH}Tools\\";
		public static readonly string APPLICATION_CONFIG_PATH = $"{APPLICATION_DATA_PATH}Config\\";

	}
}
