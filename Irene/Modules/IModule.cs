using System;

namespace Irene.Modules {
	interface IModule {
		public static string help()
			{ throw new NotImplementedException(); }
		public static void run_command(Command cmd)
			{ throw new NotImplementedException(); }
	}
}
