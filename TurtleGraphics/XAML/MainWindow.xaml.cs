﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Igor.Models;
using static TurtleGraphics.Helpers;

namespace TurtleGraphics {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged {

		#region Notifications

		public event PropertyChangedEventHandler PropertyChanged;

		private void Notify(string prop) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
		}

		#endregion


		#region Bindings

		private string _color;
		private double _brushSize;
		private Point _startPoint;
		private ICommand _runCommand;
		private string _commandsText = "";
		private double _angle;
		private double _x;
		private double _y;
		private int _delay = 0;
		private bool _penDown;
		private int _iterationCount = 1;
		private ICommand _buttonCommand;
		private ICommand _stopCommand;
		private string _buttonText = "Run";
		private ICommand _toggleFullScreenCommand;
		private bool _toggleFullscreenEnabled = true;
		private string _buttonTextFullSize = "Run on fullsize canvas";
		private ICommand _buttonFullSizeCommand;
		private bool _showTurtleCheckBox = true;
		private string _inteliCommandsText;
		private ICommand _saveCommand;
		private ICommand _loadCommand;
		private int _anotherDelay = 1;

		public int CalculationFramesPreUIUpdate { get => _anotherDelay; set { _anotherDelay = value; Notify(nameof(CalculationFramesPreUIUpdate)); } }
		public ICommand LoadCommand { get => _loadCommand; set { _loadCommand = value; Notify(nameof(LoadCommand)); } }
		public ICommand SaveCommand { get => _saveCommand; set { _saveCommand = value; Notify(nameof(SaveCommand)); } }
		public string InteliCommandsText { get => _inteliCommandsText; set { _inteliCommandsText = value; Notify(nameof(InteliCommandsText)); } }
		public bool ShowTurtleCheckBox { get => _showTurtleCheckBox; set { _showTurtleCheckBox = value; Notify(nameof(ShowTurtleCheckBox)); } }
		public ICommand ButtonFullSizeCommand { get => _buttonFullSizeCommand; set { _buttonFullSizeCommand = value; Notify(nameof(ButtonFullSizeCommand)); } }
		public string ButtonTextFullSize { get => _buttonTextFullSize; set { _buttonTextFullSize = value; Notify(nameof(ButtonTextFullSize)); } }
		public bool ToggleFullscreenEnabled { get => _toggleFullscreenEnabled; set { _toggleFullscreenEnabled = value; Notify(nameof(ToggleFullscreenEnabled)); } }
		public ICommand ToggleFullScreenCommand { get => _toggleFullScreenCommand; set { _toggleFullScreenCommand = value; Notify(nameof(ToggleFullScreenAction)); } }
		public string ButtonText { get => _buttonText; set { _buttonText = value; Notify(nameof(ButtonText)); } }
		public ICommand StopCommand { get => _stopCommand; set { _stopCommand = value; Notify(nameof(StopCommand)); } }
		public ICommand ButtonCommand { get => _buttonCommand; set { _buttonCommand = value; Notify(nameof(ButtonCommand)); } }
		public int IterationCount { get => _iterationCount; set { _iterationCount = value; Notify(nameof(IterationCount)); } }
		public bool PenDown { get => _penDown; set { if (value == _penDown) return; _penDown = value; NewPath(); Notify(nameof(PenDown)); } }
		public int PathAnimationFrames { get => _delay; set { _delay = value; Notify(nameof(PathAnimationFrames)); } }
		public double Y { get => _y; set { _y = value; Notify(nameof(Y)); } }
		public double X { get => _x; set { _x = value; Notify(nameof(X)); } }
		public double Angle { get => _angle; set { _angle = value; Notify(nameof(Angle)); } }
		public string CommandsText { get => _commandsText; set { _commandsText = value; Notify(nameof(CommandsText)); } }
		public ICommand RunCommand { get => _runCommand; set { _runCommand = value; Notify(nameof(RunCommand)); } }
		public Point StartPoint { get => _startPoint; set { _startPoint = value; Notify(nameof(StartPoint)); } }
		public double BrushSize { get => _brushSize; set { if (value == _brushSize) return; _brushSize = value; NewPath(); Notify(nameof(BrushSize)); } }
		public string Color { get => _color; set { if (value == _color) return; _color = value; NewPath(); Notify(nameof(Color)); } }


		#endregion


		private Path _currentPath;
		private PathFigure _currentFigure;
		private CancellationTokenSource cancellationTokenSource;
		private bool _inteliCommandsEnabled = true;
		private InteliCommandsHandler _inteliCommands = new InteliCommandsHandler();
		private ScrollViewer _inteliCommandsScroller;
		private readonly CompilationStatus _compilationStatus = new CompilationStatus();


		public double DrawWidth { get; set; }
		public double DrawHeight { get; set; }
		public bool AnimatePath { get; set; }
		public static MainWindow Instance { get; set; }
		public FileSystemManager FSSManager { get; set; }
		public bool SaveDialogActive { get; set; }
		public bool LoadDialogActive { get; set; }

		public MainWindow() {
			InitializeComponent();
			RunCommand = new AsyncCommand(RunCommandAction);
			StopCommand = new Command(() => {
				cancellationTokenSource.Cancel();
				ButtonCommand = RunCommand;
				ButtonText = "Run";
			});
			ButtonCommand = RunCommand;
			ToggleFullScreenCommand = new Command(ToggleFullScreenAction);
			ButtonFullSizeCommand = new Command(async () => {
				if (ButtonCommand == RunCommand) {
					ControlArea.Width = new GridLength(0, GridUnitType.Pixel);
					await Task.Delay(1);
					DrawWidth = DrawAreaX.ActualWidth;
					IterationCount = 1;
					PathAnimationFrames = 1;
					ButtonCommand.Execute(null);
					PreviewKeyDown += MainWindow_KeyDown;
				}
			});
			Loaded += MainWindow_Loaded;
			FSSManager = new FileSystemManager();
			SaveCommand = new Command(() => {
				if (SaveDialogActive || LoadDialogActive)
					return;
				SaveDialogActive = true;
				SaveDialog d = new SaveDialog();
				Grid.SetColumn(d, 1);
				Paths.Children.Add(d);
			});
			LoadCommand = new Command(async () => {
				if (SaveDialogActive || LoadDialogActive)
					return;
				LoadDialogActive = true;
				SavedData data = await FSSManager.Load();
				LoadDialogActive = false;
				if (data.Name != null) {
					CommandsText = data.Code;
				}
			});
			SizeChanged += MainWindow_SizeChanged;
			CommandsTextInput.SelectionChanged += CommandsTextInput_SelectionChanged;
			DataContext = this;
			Instance = this;
		}


		public void Init() {
			List<UIElement> toRemove = new List<UIElement>();
			foreach (UIElement child in Paths.Children) {
				if (child.GetType() == typeof(Path)) {
					toRemove.Add(child);
				}
			}

			foreach (var item in toRemove) {
				Paths.Children.Remove(item);
			}
			_currentFigure = null;
			_currentPath = null;

			Color = "Blue";
			PenDown = true;
			X = DrawWidth / 2;
			Y = DrawHeight / 2;
			TurtleTranslation.X = X;
			TurtleTranslation.Y = Y;
			TurtleRotation.Angle = 90;
			TurtleScale.ScaleX = 1;
			TurtleScale.ScaleY = 1;
			StartPoint = new Point(X, Y);
			Angle = Math.PI / 2;
			BrushSize = 4;
			PathAnimationFrames = 5;

			NewPath();
		}


		#region Events

		private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
			DrawWidth = DrawAreaX.ActualWidth;
			DrawHeight = DrawAreaY.ActualHeight;
			_inteliCommandsScroller = FindDescendant<ScrollViewer>(InteliCommands);
			Init();
			Loaded -= MainWindow_Loaded;
		}


		private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) {
			DrawWidth = DrawAreaX.ActualWidth;
			DrawHeight = DrawAreaY.ActualHeight;
			Init();
		}

		private async void MainWindow_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				ControlArea.Width = new GridLength(1, GridUnitType.Star);
				await Task.Delay(1);
				DrawWidth = DrawAreaX.ActualWidth;
				PreviewKeyDown -= MainWindow_KeyDown;
			}
		}

		private void CommandsTextInput_SelectionChanged(object sender, RoutedEventArgs e) {
			if (_inteliCommandsEnabled) {
				_inteliCommands.Handle(this, CommandsTextInput);
				Notify(nameof(CommandsText));
				Notify(nameof(InteliCommandsText));
			}
		}


		private void CommandsTextInput_ScrollChanged(object sender, ScrollChangedEventArgs e) {
			Console.WriteLine(e.VerticalOffset);
			if (_inteliCommandsScroller == null) {
				return;
			}
			_inteliCommandsScroller.ScrollToVerticalOffset(e.VerticalOffset);
		}

		#endregion


		public void NewPath() {
			if (_currentPath != null) {
				_currentPath.Data.Freeze();
			}

			_currentPath = new Path();
			Grid.SetColumn(_currentPath, 1);

			if (!PenDown) {
				_currentPath.Stroke = Brushes.Transparent;
			}
			else {
				_currentPath.Stroke = (Brush)new BrushConverter().ConvertFromString(Color);
			}
			_currentPath.StrokeThickness = BrushSize;
			_currentPath.StrokeEndLineCap = PenLineCap.Round;
			_currentPath.StrokeStartLineCap = PenLineCap.Round;

			PathGeometry pGeometry = new PathGeometry();
			pGeometry.Figures = new PathFigureCollection();
			_currentFigure = new PathFigure {
				StartPoint = new Point(X, Y),
				Segments = new PathSegmentCollection()
			};

			pGeometry.Figures.Add(_currentFigure);
			_currentPath.Data = pGeometry;
			Paths.Children.Add(_currentPath);
		}

		public void Rotate(double angle, bool setRotation) {
			if (double.IsNaN(angle)) {
				Angle = 0;
				TurtleRotation.Angle = 90;
				return;
			}
			if (setRotation) {
				Angle = ContextExtensions.AsRad(angle);
				TurtleRotation.Angle = 90 - angle;
			}
			else {
				Angle += ContextExtensions.AsRad(angle);
				TurtleRotation.Angle -= angle;
			}
			if (Angle > 2 * Math.PI) {
				Angle -= 2 * Math.PI;
			}
		}

		public async Task Forward(double length) {
			double targetX = X + Math.Sin(Angle) * length;
			double targetY = Y + Math.Cos(Angle) * length;

			await Draw(new Point(targetX, targetY));
		}

		#region Drawing lines

		private async Task DrawData(List<TurtleData> compiledTasks) {
			Init();

			for (int i = 0; i < compiledTasks.Count; i++) {
				if (cancellationTokenSource.Token.IsCancellationRequested) {
					return;
				}

				TurtleData data = compiledTasks[i];
				if (i % CalculationFramesPreUIUpdate == 0) {
					await Task.Delay(1);
				}
				switch (data.Action) {
					case ParsedAction.NONE: { break; }
					case ParsedAction.Forward: {
						await Forward(data.Distance);
						break;
					}
					case ParsedAction.Rotate: {
						Rotate(data.Angle, data.SetAngle);
						break;
					}
					case ParsedAction.MoveTo: {
						X = data.MoveTo.X;
						Y = data.MoveTo.Y;
						NewPath();
						break;
					}
					case ParsedAction.Color: {
						Color = ((SolidColorBrush)data.Brush).Color.ToString();
						NewPath();
						break;
					}
					case ParsedAction.Thickness: {
						BrushSize = data.BrushThickness;
						double scale = BrushSize / 4;
						TurtleScale.ScaleX = TurtleScale.ScaleY = scale;
						NewPath();
						break;
					}
					case ParsedAction.PenState: {
						PenDown = data.PenDown;
						NewPath();
						break;
					}
				}
			}
		}

		public async Task Draw(Point to) {
			_currentFigure.Segments.Add(new LineSegment(new Point(X, Y), true) { IsSmoothJoin = true });
			X = to.X;
			Y = to.Y;
			await Displace(to);
		}


		public async Task Displace(Point to) {
			int last = _currentFigure.Segments.Count - 1;
			LineSegment lastSegment = (LineSegment)_currentFigure.Segments[last];
			Point origin = lastSegment.Point;
			double increment = 1d / IterationCount;
			double currentInterpolation = 0;

			for (int i = 0; i <= IterationCount; i++) {
				if (cancellationTokenSource.Token.IsCancellationRequested) {
					break;
				}
				lastSegment.Point = new Point(Lerp(origin.X, to.X, currentInterpolation), Lerp(origin.Y, to.Y, currentInterpolation));
				TurtleTranslation.X = lastSegment.Point.X;
				TurtleTranslation.Y = lastSegment.Point.Y;
				_currentFigure.Segments[last] = lastSegment;
				currentInterpolation += increment;
				if (AnimatePath) {
					await Task.Delay(1);
				}
			}
			lastSegment.Freeze();
		}

		#endregion


		#region Actions

		private async Task RunCommandAction() {
			_compilationStatus.Start();
			ToggleFullscreenEnabled = false;
			Init();
			cancellationTokenSource = new CancellationTokenSource();
			ButtonCommand = StopCommand;
			ButtonText = "Stop";
			Queue<ParsedData> tasks = CommandParser.Parse(CommandsText, this);

			List<TurtleData> compiledTasks = await CompileTasks(tasks, cancellationTokenSource.Token);
			_compilationStatus.Stop();

			await DrawData(compiledTasks);

			ButtonCommand = RunCommand;
			ButtonText = "Run";
			ToggleFullscreenEnabled = true;
		}

		private Task<List<TurtleData>> CompileTasks(Queue<ParsedData> tasks, CancellationToken token) {
			return Task.Run(() => {
				List<TurtleData> ret = new List<TurtleData>(8192);

				TurtleData initial = new TurtleData() { Angle = Angle, Brush = Brushes.Blue, BrushThickness = BrushSize, MoveTo = new Point(X, Y), PenDown = true };
				ret.Add(initial);

				while (tasks.Count > 0) {
					ParsedData current = tasks.Dequeue();
					if (current.IsBlock) {
						ret.AddRange(current.CompileBlock(initial, token));
					}
					else {
						ret.Add(current.Compile(initial, token));
					}
					initial = ret[ret.Count - 1];
				}
				return ret;
			});
		}

		public void ToggleFullScreenAction() {
			if (WindowStyle == WindowStyle.SingleBorderWindow) {
				WindowStyle = WindowStyle.None;
				WindowState = WindowState.Maximized;
			}
			else {
				WindowStyle = WindowStyle.SingleBorderWindow;
				WindowState = WindowState.Normal;
			}
		}

		#endregion
	}
}
