﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Timers;

namespace Irene {
	class Logger {
		public enum Severity { Error, Warning, Info, Debug }

		// Log files are created under this directory.
		public string dir { get; }

		readonly Timer timer;
		DateTime log_epoch;
		string file;

		// Set up a new logger.
		public Logger(string dir, TimeSpan interval) {
			// Fixes character conversion issues (e.g. \u2022).
			Console.OutputEncoding = System.Text.Encoding.UTF8;

			this.dir = dir;
			timer = new (interval.TotalMilliseconds);
			timer.AutoReset = true;
			timer.Elapsed += (s, e) => new_file();

			Directory.CreateDirectory(dir);
			new_file();
			timer.Start();
		}

		// Create a new file and redirect logging output to it.
		[MemberNotNull(nameof(file))]
		void new_file() {
			log_epoch = DateTime.Now;
			string filename = log_epoch.ToString("yyyy-MM-dd_HHmm");
			file = $@"{dir}/{filename}.txt";
			StreamWriter s = File.CreateText(file);
			s.Close();
		}

		// Create a newline (no timestamps) on the console and logfile.
		public void endl() {
			Console.WriteLine();
			StreamWriter writer = File.AppendText(file);
			writer.WriteLine();
			writer.Close();
		}

		// Convenience functions for logging to various priorities.
		public void error  (string text) { log(text, Severity.Error); }
		public void warning(string text) { log(text, Severity.Warning); }
		public void info   (string text) { log(text, Severity.Info); }
		public void debug  (string text) { log(text, Severity.Debug); }

		// Logs the given text (both to the console and to the logfile).
		public void log(string text, Severity level) {
			DateTime now = DateTime.Now;
			print_console(text, level, now);
			print_file(text, level, now);
		}

		// Log to the console.
		static void print_console(string text, Severity level, DateTime time) {
			lock (new object()) {
				string time_str = time.ToString(@"H:mm:ss");
				write_colored($"{time_str} ", ConsoleColor.DarkGray);

				switch (level) {
				case Severity.Error:
					write_colored("[ERROR]", ConsoleColor.Black, ConsoleColor.Red);
					break;
				case Severity.Warning:
					write_colored("[WARN]", ConsoleColor.DarkYellow);
					break;
				case Severity.Info:
					write_colored("[info]", ConsoleColor.DarkGray);
					break;
				case Severity.Debug:
					write_colored("[dbug]", ConsoleColor.DarkGray);
					break;
				}
				write(" ");

				if (level == Severity.Debug) {
					write_colored(text, ConsoleColor.DarkGray);
				} else {
					write(text);
				}

				Console.Write("\n");
			}
		}

		// Log to the designated file.
		void print_file(string text, Severity level, DateTime time) {
			string time_str = time.ToString("yyyy-MM-dd H:mm:ss.ff");
			string tag = level switch {
				Severity.Error   => "[ERR ]",
				Severity.Warning => "[WARN]",
				Severity.Info    => "[info]",
				Severity.Debug   => "[dbug]",
				_ => "[?]",
			};
			string entry = $"{time_str} > {tag} {text}";

			lock (new object()) {
				StreamWriter writer = File.AppendText(file);
				writer.WriteLine(entry);
				writer.Close();
			}
		}

		// Alias for `Console.Write`.
		static void write(string text) { Console.Write(text); }

		// Uses `Console.Write` in a specific color combo, and
		// restores the colors after writing.
		static void write_colored(string text, ConsoleColor fg, ConsoleColor bg = ConsoleColor.Black) {
			lock (new object()) {
				ConsoleColor fg_prev = Console.ForegroundColor;
				ConsoleColor bg_prev = Console.BackgroundColor;
				Console.ForegroundColor = fg;
				Console.BackgroundColor = bg;
				Console.Write(text);
				Console.ForegroundColor = fg_prev;
				Console.BackgroundColor = bg_prev;
			}
		}
	}
}
