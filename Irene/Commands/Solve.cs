using System.Net.Http;

using OpenCvSharp;

namespace Irene.Commands;

class Solve : AbstractCommand, IInit {
	// L: Lower-triangular matrix
	// U: Upper-triangular matrix
	// P: Permutation matrix
	private readonly record struct LUP
		(int[][] L, int[][] U, int[][] P);

	private static readonly HttpClient _http;

	// See: https://matrixcalc.org/en/
	// Base matrix:
	// 1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 0,0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 0,0,0,1,1,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 1,0,0,0,0,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 0,1,0,0,0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0
	// 0,0,1,0,0,0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0
	// 0,0,0,1,0,0,0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0
	// 0,0,0,0,1,0,0,0,1,1,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0
	// 0,0,0,0,0,1,0,0,0,0,1,1,0,0,0,1,0,0,0,0,0,0,0,0,0
	// 0,0,0,0,0,0,1,0,0,0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0
	// 0,0,0,0,0,0,0,1,0,0,0,1,1,1,0,0,0,1,0,0,0,0,0,0,0
	// 0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0,0,0,1,0,0,0,0,0,0
	// 0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,0,0,0,0,1,0,0,0,0,0
	// 0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,1,1,0,0,0,1,0,0,0,0
	// 0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0,0,0,1,0,0,0
	// 0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0,0,0,1,0,0
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0,0,0,1,0
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,0,0,0,0,1
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,1,1,0,0,0
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0,0
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1,1
	// 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,1
	private static readonly LUP _lupSolver;
	private const double _roundoff = 0.000005f; // 1/56 is ~0.017

	private const string _dirFiles = @"temp/";
	private const string _prefixFiles = "solve";
	private const string _separatorFiles = "-";

	public static void Init() { }
	static Solve() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		// Ensure temp directory for image processing exists.
		Directory.CreateDirectory(_dirFiles);

		// HttpClient should only be shared.
		_http = new ();
		
		// Initialize solver.
		int[][] solver_base = new int[25][] {
			new int[25] { 1,1,0,0,0, 1,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 1,1,1,0,0, 0,1,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,1,1,1,0, 0,0,1,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,1,1,1, 0,0,0,1,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,1,1, 0,0,0,0,1, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },

			new int[25] { 1,0,0,0,0, 1,1,0,0,0, 1,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,1,0,0,0, 1,1,1,0,0, 0,1,0,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,1,0,0, 0,1,1,1,0, 0,0,1,0,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,1,0, 0,0,1,1,1, 0,0,0,1,0, 0,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,0,1, 0,0,0,1,1, 0,0,0,0,1, 0,0,0,0,0, 0,0,0,0,0 },

			new int[25] { 0,0,0,0,0, 1,0,0,0,0, 1,1,0,0,0, 1,0,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,1,0,0,0, 1,1,1,0,0, 0,1,0,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,1,0,0, 0,1,1,1,0, 0,0,1,0,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,1,0, 0,0,1,1,1, 0,0,0,1,0, 0,0,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,1, 0,0,0,1,1, 0,0,0,0,1, 0,0,0,0,0 },

			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 1,0,0,0,0, 1,1,0,0,0, 1,0,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,1,0,0,0, 1,1,1,0,0, 0,1,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,1,0,0, 0,1,1,1,0, 0,0,1,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,1,0, 0,0,1,1,1, 0,0,0,1,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,1, 0,0,0,1,1, 0,0,0,0,1 },

			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 1,0,0,0,0, 1,1,0,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,1,0,0,0, 1,1,1,0,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,1,0,0, 0,1,1,1,0 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,1,0, 0,0,1,1,1 },
			new int[25] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,1, 0,0,0,1,1 },
		};
		_lupSolver = DecomposeToLUP(solver_base);

		Log.Information("  Initialized command: /solve");
		Log.Debug("    Downloader & file store initialized.");
		Log.Debug("    LU decomposition calculated.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`/solve <puzzle>` solves an in-game puzzle.",
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"solve",
				"Solve an in-game puzzle.",
				new List<CommandOption> {
					new (
						"mezzonic-lock",
						"Solve a Mezzonic puzzle (a.k.a. lights out).",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"screenshot",
								"A screenshot of your game window.",
								ApplicationCommandOptionType.Attachment,
								required: true
							),
						}
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferEphemeralAsync, RunAsync )
		};

	public static async Task RunAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();

		DiscordAttachment attachment =
			interaction.Interaction.GetTargetAttachment();

		string filename = string.Join(_separatorFiles,
			_prefixFiles,
			attachment.Id,
			attachment.FileName
		);
		filename = _dirFiles + filename;

		HttpResponseMessage http_response =
			await _http.GetAsync(attachment.Url);
		Stream file_http = await
			http_response.Content.ReadAsStreamAsync();
		using (FileStream file = File.Create(filename)) {
			file_http.Position = 0;
			await file_http.CopyToAsync(file);
		}

		int[][]? grid = null;
		try {
			grid = ExtractGrid(filename);
		} catch { }

		if (grid is null) {
			await Command.SubmitResponseAsync(
				interaction,
				"""
				Could not extract grid. :fearful:
				If you haven't, try hiding your UI? (default: `Alt`+`Z`)
				""",
				"Failed to extract grid.",
				LogLevel.Debug,
				"Grid extraction failed.".AsLazy()
			);
			return;
		}

		string response = "**Detected grid:**\n";
		for (int row=0; row<5; row++) {
			for (int col=0; col<5; col++) {
				response += (grid[row][col] == 1)
					? ":blue_square:"
					: ":black_large_square:";
			}
			if (row+1 < 5)
				response += "\n";
		}

		int[][] solution = SolveGrid(grid);
		response += "\n\n**Minimal solution:**\n";
		for (int row = 0; row<5; row++) {
			for (int col = 0; col<5; col++) {
				response += (solution[row][col] == 1)
					? ":red_square:"
					: ":black_large_square:";
			}
			if (row+1 < 5)
				response += "\n";
		}

		response += "\n\n" +
			"""
			*If the grid detected is wrong and your UI is visible, try hiding your UI.* (default: `Alt`+`Z`)
			*If the solution didn't work, try exiting and getting a new puzzle (this doesn't cost energy).*
			""";

		await Command.SubmitResponseAsync(
			interaction,
			response,
			"Image processed.",
			LogLevel.Debug,
			"Grid extracted".AsLazy()
		);

		File.Delete(filename);
	}

	private static int[][] ExtractGrid(string filename) {
		using ResourcesTracker t = new ();

		// Convert into HSV and split to separate channels.
		Mat image = t.T(new Mat(filename));
		image = t.T(image.CvtColor(ColorConversionCodes.BGR2HSV));
		Mat[] image_split = t.T(image.Split());
		Mat image_H = t.T(image_split[0]);
		Mat image_S = t.T(image_split[1]);
		Mat image_V = t.T(image_split[2]);

		// Filter for specific H/S ranges.
		Mat image_H_filtered = t.T(new Mat());
		Cv2.InRange(image_H,  10,  20, image_H_filtered);
		Mat image_S_filtered = t.T(new Mat());
		Cv2.InRange(image_S, 120, 140, image_S_filtered);
		Mat filtered = t.T(new Mat());
		Cv2.Multiply(image_H_filtered, image_S_filtered, filtered);

		// Filter for V channel (grid only).
		Mat image_V_filtered = t.T(new Mat());
		Cv2.InRange(image_V, 120, 255, image_V_filtered);

		// Find frame bounding box.
		Mat smoothed_frame = t.T(new Mat());
		int size_erode_frame = 5;
		Mat kernel_erode_frame = t.T(Cv2.GetStructuringElement(
			MorphShapes.Rect,
			new Size(size_erode_frame*2+1, size_erode_frame*2+1),
			new Point(size_erode_frame, size_erode_frame)
		));
		Cv2.MorphologyEx(
			filtered,
			smoothed_frame,
			MorphTypes.Open,
			kernel_erode_frame
		);
		Cv2.MorphologyEx(
			smoothed_frame,
			smoothed_frame,
			MorphTypes.Close,
			kernel_erode_frame
		);

		// Filter for inner grid.
		Mat smoothed_grid = t.T(new Mat());
		int size_erode_grid = 5;
		Mat kernel_erode_grid = t.T(Cv2.GetStructuringElement(
			MorphShapes.Ellipse,
			new Size(size_erode_grid*2+1, size_erode_grid*2+1),
			new Point(size_erode_grid, size_erode_grid)
		));
		Cv2.MorphologyEx(
			filtered,
			smoothed_grid,
			MorphTypes.Close,
			kernel_erode_grid
		);

		// Edge detection (for outer frame).
		Mat edges_frame = t.T(new Mat());
		double threshold_canny_frame = 10;
		Cv2.Canny(
			smoothed_frame,
			edges_frame,
			threshold_canny_frame,
			threshold_canny_frame*3
		);
		Cv2.FindContours(
			smoothed_frame,
			out Point[][] contours_frame,
			out HierarchyIndex[] hierarchy_frame,
			RetrievalModes.External,
			ContourApproximationModes.ApproxSimple
		);

		// Create list of contours and sort by inner area.
		List<Point[]> contours_frame_list = new ();
		for (int i = 0; i<contours_frame.Length; i++)
			contours_frame_list.Add(contours_frame[i]);
		contours_frame_list.Sort((x, y) => {
			return Math.Sign(
				Cv2.ContourArea(y)-Cv2.ContourArea(x)
			);
		});

		// Visualize largest-area contour (frame).
		Mat visualize = t.T(
			Mat.Zeros(edges_frame.Size(), MatType.CV_8UC3)
		);
		Cv2.DrawContours(
			visualize,
			contours_frame_list, 0,
			Scalar.ForestGreen, 3
		);

		// Get bounding box of frame + inner-mask.
		// Sample values:
		//   1160x870
		//    690x550
		Rect frame_box = Cv2.BoundingRect(contours_frame_list[0]);
		double frame_h = frame_box.Height;
		double frame_w = frame_box.Width;
		double inner_h = frame_h * 0.63;
		double inner_w = frame_w * 0.59;
		Rect inner_box = new (
			(int)Math.Round(frame_box.Left + (frame_w-inner_w)/2),
			(int)Math.Round(frame_box.Top + (frame_h-inner_h)/2),
			(int)Math.Round(inner_w),
			(int)Math.Round(inner_h)
		);
		Cv2.Rectangle(visualize, frame_box, Scalar.Orchid, 1);
		Cv2.Rectangle(visualize, inner_box, Scalar.Orchid, 5);

		// Extract inner grid sub-texture.
		Mat smoothed_inner = t.T(smoothed_grid.SubMat(inner_box));
		Mat image_V_inner = t.T(image_V_filtered.SubMat(inner_box));
		
		// Edge detection (for inner grid).
		Mat edges_grid = t.T(new Mat());
		double threshold_canny_grid = 10;
		Cv2.Canny(
			smoothed_inner,
			edges_grid,
			threshold_canny_grid,
			threshold_canny_grid*3
		);
		Cv2.FindContours(
			smoothed_inner,
			out Point[][] contours_grid,
			out HierarchyIndex[] hierarchy_grid,
			RetrievalModes.List,
			ContourApproximationModes.ApproxSimple
		);

		// Create list of contours and sort by inner area.
		List<Point[]> contours_grid_list = new ();
		for (int i = 0; i<contours_grid.Length; i++)
			contours_grid_list.Add(contours_grid[i]);
		contours_grid_list.Sort((x, y) => {
			return Math.Sign(
				Cv2.ContourArea(y)-Cv2.ContourArea(x)
			);
		});

		// Calculate coordinates of all grid elements.
		// Sample values:
		//  478x478
		//   96x96
		//   60x60
		Rect grid_box = Cv2.BoundingRect(contours_grid_list[0]);
		int grid_h = grid_box.Height;
		int grid_w = grid_box.Width;
		float element_h = grid_h/5.0f;
		float element_w = grid_w/5.0f;
		float subelement_h = element_h*0.6f;
		float subelement_w = element_w*0.6f;

		// Initialize output grid.
		int[][] grid = new int[5][];
		for (int i=0; i<5; i++)
			grid[i] = new int[5];

		// Iterate through each subelement and check if on/off.
		Mat image_V_grid = t.T(image_V_inner.SubMat(grid_box));
		for (int row=0; row<5; row++) {
			for (int col=0; col<5; col++) {
				Rect2f element_box_float = new (
					col*element_w + (element_w-subelement_w)/2,
					row*element_h + (element_h-subelement_h)/2,
					subelement_w,
					subelement_h
				);
				Rect element_box = new (
					(int)Math.Round(element_box_float.Left),
					(int)Math.Round(element_box_float.Top),
					(int)Math.Round(element_box_float.Width),
					(int)Math.Round(element_box_float.Height)
				);
				Mat element = t.T(image_V_grid.SubMat(element_box));
				double mean = element.Mean().Val0;
				double threshold = 85.0;
				grid[row][col] = (mean > threshold) ? 1 : 0;
			}
		}

		return grid;
	}

	private static int[][] SolveGrid(int[][] grid) {
		// Populate augment of base matrix.
		int rows = grid.Length;
		int cols = grid[0].Length;
		int[] augment = new int[rows * cols];
		for (int row=0; row<rows; row++) {
			for (int col=0; col<cols; col++)
				augment[row*cols + col] = grid[row][col];
		}

		// Solve.
		int[] solution_raw = SolveByLUP(augment);

		// Format solution as grid.
		int[][] solution0 = new int[rows][];
		for (int row=0; row<rows; row++) {
			solution0[row] = new int[cols];
			for (int col=0; col<cols; col++)
				solution0[row][col] = solution_raw[row*cols + col];
		}

		// Define null space (solvable systems will always have 2
		// extra degrees of freedom).
		int[][] null1 = new int[5][] {
			new int[5] { 0,1,1,1,0 },
			new int[5] { 1,0,1,0,1 },
			new int[5] { 1,1,0,1,1 },
			new int[5] { 1,0,1,0,1 },
			new int[5] { 0,1,1,1,0 },
		};
		int[][] null2 = new int[5][] {
			new int[5] { 1,0,1,0,1 },
			new int[5] { 1,0,1,0,1 },
			new int[5] { 0,0,0,0,0 },
			new int[5] { 1,0,1,0,1 },
			new int[5] { 1,0,1,0,1 },
		};

		// Hard-coded matrix addition helper function.
		static int[][] ModuloAdd(int[][] x, int[][] y) {
			int[][] sum = new int[5][];
			for (int i=0; i<5; i++) {
				sum[i] = new int[5];
				for (int j=0; j<5; j++)
					sum[i][j] = (x[i][j] + y[i][j]) % 2;
			}
			return sum;
		}

		// Construct other 3 solutions.
		int[][] solution1 = ModuloAdd(solution0, null1);
		int[][] solution2 = ModuloAdd(solution0, null2);
		int[][] solution3 = ModuloAdd(solution1, null2);

		// Hard-coded summation helper function.
		static int Clicks(int[][] solution) {
			int sum = 0;
			for (int i=0; i<5; i++) {
				for (int j=0; j<5; j++)
					sum += solution[i][j];
			}
			return sum;
		}

		// Sort solutions by number of clicks.
		List<int[][]> solutions = new ()
			{ solution0, solution1, solution2, solution3 };
		solutions.Sort((x, y) => { return Clicks(x)-Clicks(y); });

		// Return top (least clicks) solution.
		//return solution0;
		return solutions[0];
	}

	// Another way to solve this would be with the pseudoinverse.
	// See: https://srabbani.com/lights_out.pdf
	//   0,0,0,0,0, 1,0,0,0,0, 1,1,0,0,0, 1,0,1,0,0, 0,1,1,1,0
	//   0,0,0,0,0, 0,1,0,0,0, 1,1,1,0,0, 0,0,0,1,0, 1,1,0,1,1
	//   0,0,0,1,0, 0,0,0,1,1, 0,0,1,1,0, 0,1,0,1,0, 1,1,1,0,1
	//   0,0,1,1,1, 0,1,0,0,0, 1,1,0,1,1, 0,1,0,0,0, 0,0,1,1,1
	//   0,0,0,1,1, 0,0,1,0,1, 0,1,1,1,0, 1,0,0,0,0, 1,0,1,1,0
	//
	//   0,0,1,0,1, 0,1,1,0,1, 0,0,1,0,0, 0,0,0,1,1, 0,0,0,0,0
	//   0,0,1,0,0, 0,1,1,1,0, 1,1,0,0,1, 0,1,0,0,1, 0,0,1,1,0
	//   0,0,0,0,1, 0,0,0,1,1, 0,0,0,0,1, 0,0,0,0,0, 0,0,0,0,1
	//   0,0,1,0,0, 0,1,1,1,0, 1,0,0,1,1, 1,0,0,1,0, 0,1,1,0,0
	//   0,0,1,0,1, 0,1,1,0,1, 1,0,1,0,1, 1,1,0,0,0, 1,0,0,0,1
	//
	//   0,0,0,0,1, 0,0,0,1,1, 0,0,1,0,1, 1,1,1,1,0, 0,1,0,0,0
	//   0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,1,0,0,0, 1,1,1,0,0
	//   0,0,0,1,1, 0,0,1,0,0, 0,1,1,0,1, 1,0,0,0,1, 1,0,1,1,0
	//   0,0,1,1,1, 0,1,0,1,0, 1,1,1,0,0, 0,0,0,1,0, 1,1,0,1,1
	//   0,0,0,1,0, 0,0,1,1,1, 0,1,0,0,0, 1,1,0,1,0, 0,1,0,1,1
	//
	//   0,0,1,0,0, 0,1,1,1,0, 1,0,0,0,1, 1,0,1,0,1, 1,0,1,0,0
	//   0,0,1,1,0, 0,1,0,0,1, 1,1,0,0,1, 0,1,1,1,0, 0,0,1,0,0
	//   0,0,1,0,1, 0,1,1,0,1, 1,0,1,0,0, 1,1,0,1,1, 1,0,0,0,0
	//   0,0,0,1,0, 0,0,1,1,1, 0,1,0,0,0, 1,1,0,1,1, 0,1,0,1,0
	//   0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,1
	//
	//   0,0,0,1,1, 0,0,1,0,0, 0,1,1,0,1, 1,0,1,0,1, 1,1,0,0,0
	//   0,0,1,1,1, 0,1,0,1,0, 1,1,1,0,0, 0,0,0,0,0, 1,1,1,0,0
	//   0,0,0,1,0, 0,0,1,1,1, 0,1,0,0,0, 1,1,0,1,1, 0,1,0,0,0
	//   0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0
	//   0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0
	private static int[] SolveByLUP(int[] augment) {
		int size = _lupSolver.P.Length;

		// Ax = b, where:
		//   A is the base solver matrix
		//   x is the solution vector
		//   b is the augment vector
		// Using a LUP decomposition (PA = LU) (see record LUP):
		//   PAx = Pb
		//   LUx = Pb
		// We use an intermediate vector, y, to represent Ux:
		//   Ly = Pb
		//   Ux = y

		// Calculate Pb (and just write it as b).
		int[] b = new int[size];
		for (int i=0; i<size; i++) {
			b[i] = 0;
			for (int j=0; j<size; j++)
				b[i] += _lupSolver.P[i][j] * augment[j];
			b[i] %= 2;
		}

		// First we solve for intermediate y (Ly=b).
		int[] y = new int[size];
		for (int i=0; i<size; i++) {
			int b_i = b[i];
			for (int j=0; j<i; j++)
				b_i -= _lupSolver.L[i][j] * y[j];
			// Main diagonal of L is always 1.
			//y[i] = b_i / _lupSolver.L[i][i];
			y[i] = ((b_i % 2) + 2) % 2;
			if (Math.Abs(y[i]) < _roundoff)
				y[i] = 0;
		}

		// Next solve for x (Ux=y).
		int[] x = new int[size];
		for (int i=size-1; i>=0; i--) {
			int y_i = y[i];
			for (int j=i+1; j<size; j++)
				y_i -= _lupSolver.U[i][j] * x[j];
			y_i = ((y_i % 2) + 2) % 2;
			// If main diagonal of U is 0, use simplest solution (0).
			if (Math.Abs(_lupSolver.U[i][i]) < _roundoff)
				x[i] = 0;
			else if (Math.Abs(y_i) < _roundoff)
				x[i] = 0; // Filter out potential float residual.
			else
				x[i] = y_i / _lupSolver.U[i][i];
		}

		return x;
	}

	private static LUP DecomposeToLUP(int[][] matrix) {
		int size = matrix.Length;
		int[][] L = Identity(size);
		int[][] U = Copy(matrix);
		int[][] P = Identity(size);

		for (int i=0; i<size; i++) {
			// Find pivot row (partial pivot).
			// Finds max first element relative to rest of row.
			int max = 0;
			int i_pivot = i;
			for (int j = i; j<size; j++) {
				int max_i = Math.Abs(U[j][i]);
				if (max_i > max) {
					max = max_i;
					i_pivot = j;
				}
			}
			if (max < _roundoff)
				continue;

			// Pivot.
			if (i_pivot != i) {
				P = SwapRows(P, i, i_pivot);
				U = SwapRows(U, i, i_pivot);
				// For the rows being swapped in L, only swap the
				// first `i` places (stay below main diagonal).
				// Places beyond that should either still be 0
				// (unpopulated), or 1 (identity matrix).
				for (int j=0; j<i; j++) {
					(L[i][j], L[i_pivot][j]) =
						(L[i_pivot][j], L[i][j]);
				}
			}

			for (int j=i+1; j<size; j++) {
				// Calculate the outermost column of L by scaling,
				// starting on the row below the pivot.
				// U[i][i] cannot be 0 (or we would have skipped
				// this iteration).
				L[j][i] = U[j][i] / U[i][i];

				// Update U with subtracted values.
				// Everything below the main diagonal should be 0.
				int[] row_subtract = ScaleRow(U, -L[j][i], i);
				U = AddRow(U, row_subtract, j);
			}
		}

		// Sanitization is only needed for non-integer matrices.
		//// Sanitize all values below float threshold.
		//// Only U needs to be sanitized; all other matrices are
		//// constructed and are as accurate as possible.
		//for (int i=0; i<size; i++) {
		//	for (int j=0; j<size; j++) {
		//		if (Math.Abs(U[i][j]) < _roundoff)
		//			U[i][j] = 0;
		//	}
		//}

		return new LUP(L, U, P);
	}

	// Matrix manipulation helper functions.
	// `row` arguments are a 0-based indices to `matrix` rows.
	private static int[][] Identity(int size) {
		int[][] identity = new int[size][];
		for (int i=0; i<size; i++) {
			identity[i] = new int[size];
			for (int j=0; j<size; j++)
				identity[i][j] = (i == j) ? 1 : 0;
		}
		return identity;
	}
	private static int[][] Copy(int[][] matrix) {
		int size = matrix.Length;
		int[][] copy = new int[size][];
		for (int row=0; row<size; row++) {
			copy[row] = new int[size];
			for (int col=0; col<size; col++)
				copy[row][col] = matrix[row][col];
		}
		return copy;
	}
	private static int[] ScaleRow(int[][] matrix, int scale, int row) {
		int[] row_scaled = new int[matrix[row].Length];
		for (int i=0; i<row_scaled.Length; i++)
			row_scaled[i] = scale * matrix[row][i];
		return row_scaled;
	}
	private static int[][] AddRow(int[][] matrix, int[] add, int row) {
		for (int i=0; i<add.Length; i++) {
			int x = (((matrix[row][i] + add[i]) % 2) + 2) % 2;
			matrix[row][i] = x;
			//if (Math.Abs(matrix[row][i]) < _roundoff)
			//	matrix[row][i] = 0;
		}
		return matrix;
	}
	private static int[][] SwapRows(int[][] matrix, int row_a, int row_b) {
		for (int i=0; i<matrix[row_a].Length; i++) {
			(matrix[row_a][i], matrix[row_b][i]) =
				(matrix[row_b][i], matrix[row_a][i]);
		}
		return matrix;
	}
}

//// Used to view matrices in debugger.
//// In watch window, use value (e.g.):
////   L.Debug()
//public static class DebugExtensions {
//	public static string Debug(this double[][] array, int pad=5) {
//		var result = "";
//		for (int i=0; i<25; i++) {
//			for (int j=0; j<25; j++)
//				result += array[i][j].ToString("f1").PadLeft(pad);
//			result += "\n";
//		}
//		return result;
//	}
//}
