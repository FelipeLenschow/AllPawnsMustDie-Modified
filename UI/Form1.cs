﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AllPawnsMustDie
{
    /// <summary>
    /// This is the main Window (Form) for the Application.  A minimal amount of
    /// work should be done here, routing most UI events to a subclass to deal
    /// with.  This is (or runs on) the UI thread, so no blocking work.
    /// </summary>
    public partial class APMD_Form : Form
    {

        static SerialPort _port;

        /// <summary>
        /// Modification to initialize the serial port
        /// </summary>
        public SerialPort Serial { get; set; }



        #region Public EventArgs
        /// <summary>
        /// slef-play game has ended
        /// </summary>
        public class SelfPlayResultEventArgs : EventArgs
        {
            /// <summary>
            /// Creates a new SelfPlayResultEventArgs
            /// </summary>
            public SelfPlayResultEventArgs(bool playAgain)
            {
                // External code could keep a counter and pass false
                // when they want the loop to stop.
                keepGoing = playAgain;
            }

            /// <summary>
            /// Should the game keep going?
            /// </summary>
            public bool Continue { get { return keepGoing; } }

            private bool keepGoing;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the main form
        /// </summary>
        public APMD_Form()
        {
            InitializeComponent();
            reduceEngineStrength = false;
            upTime = new Stopwatch();
            upTime.Start();
            loadEngine();

    }
        #endregion

        #region Private Methods
        /// <summary>
        /// Form needs painting.  This routes to the ChessGame class
        /// </summary>
        /// <param name="sender">ignored</param>
        /// <param name="e">EventArgs, mostly we care about Graphics</param>
        private void APMD_Form_Paint(object sender, PaintEventArgs e)
        {
            chessGame?.Render(e.Graphics);
        }

        /// <summary>
        /// Invoked when the mouse button is released
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Used to get (X, Y) of the mouse</param>
        private void APMD_Form_MouseUp(object sender, MouseEventArgs e)
        {
            // MouseUp allows getting the button and the coordinates
            // MouseDown does as well, but Click and MouseClick do not allow both
            if (e.Button == MouseButtons.Left)
            {
                chessGame?.ProcessClick(e.X, e.Y);
            }
            //Debug.WriteLine(e.X + " " + e.Y);

        }

        /// <summary>
        /// Menu handler for "File->New Game"
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void newGameToolStripNewGame_Click(object sender, EventArgs e)
        {
            NewGameDialog newGameDialog = new NewGameDialog(NewGameDialog.NewGameType.Normal);
            DialogResult result = newGameDialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                NewGame(String.Empty, newGameDialog.Info.PlayerColor, newGameDialog.Info.ThinkTime);
            }
        }

        /// <summary>
        /// Opens a dialog to accept a FEN string.  If that is successful, it sends
        /// the FEN string to the ChessGame instance (assuming we have one)
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void newPositionToolStripNewPosition_Click(object sender, EventArgs e)
        {
            // Opens a dialog to read the FEN string and then passes it to engine
            NewGameDialog newGameDialog = new NewGameDialog(NewGameDialog.NewGameType.PositionalFEN);
            DialogResult result = newGameDialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                NewGame(newGameDialog.StartingPosition, PieceColor.White, newGameDialog.Info.ThinkTime);
            }
        }

        /// <summary>
        /// Menu handler for File->Exit
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void exitToolStripExit_Click(object sender, EventArgs e)
        {
            chessGame?.Quit();  // Quit the game
            Close();            // Close the form
        }

        /// <summary>
        /// Menu handler for "File->Load Engine..."
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void loadEngineToolStripLoadEngine_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = Properties.Resources.LoadEngineFileTitle;
            openFileDialog.Filter = "Exe Files (*.exe) | *.exe";
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                chessGame?.StopEngineSelfPlay();
                fullPathToChessExe = openFileDialog.FileName;
                Text = String.Format("All Pawns Must Die [{0}]", openFileDialog.SafeFileName);
                Invalidate();
            }
        }

        /// <summary>
        /// Autoload engine on start."
        /// </summary>
        public void loadEngine()
        {

            chessGame?.StopEngineSelfPlay();
            FileInfo engine = new FileInfo(Path.Combine(Environment.CurrentDirectory, "stockfish_14.1_win_x64_avx2.exe"));
            fullPathToChessExe = engine.FullName;
            Debug.WriteLine(fullPathToChessExe);
            //Text = String.Format("All Pawns Must Die [{0}]", openFileDialog.SafeFileName);
            Invalidate();
            
        }

        /// <summary>
        /// Menu handler for File->Self Play
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void selfPlayToolStripSelfPlay_Click(object sender, EventArgs e)
        {
            SelfPlayResultEventArgs spea = e as SelfPlayResultEventArgs;
            // Check if this is an event from our handler
            if (spea != null && (spea.Continue))
            {
                NewGame(String.Empty, PieceColor.White, selfPlayThinkTime);
                chessGame?.StartEngineSelfPlay();
            }
            else
            {
                // Otherwise, pop a new dialog for the thinktime
                NewGameDialog newGameDialog = new NewGameDialog(NewGameDialog.NewGameType.SelfPlay);
                DialogResult result = newGameDialog.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    selfPlayThinkTime = newGameDialog.Info.ThinkTime;
                    NewGame(String.Empty, PieceColor.White, selfPlayThinkTime);
                    chessGame?.StartEngineSelfPlay();
                }
            }
        }

        /// <summary>
        /// Event handler fired when a self play game has finished
        /// </summary>
        /// <param name="sender">Ignored/passed through</param>
        /// <param name="e">Ignored/passed through</param>
        private void ChessGameSelfPlayGameOverEventHandler(object sender, EventArgs e)
        {
            ReportUptime();

            // Start a new game - for now just forward it to the menu handler.
            // Really we could subcribe there directly, but this will allow more
            // to happen on this path later
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    // Start a small delay timer so we have a chance to see the
                    // board
                    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                    timer.Tick += SelfPlayDelayTick;
                    timer.Interval = 5000; // 5 seconds for now
                    timer.Start();
                });
            }
        }

        /// <summary>
        /// Callback for the selfplay delay timer
        /// </summary>
        /// <param name="sender">Timer object</param>
        /// <param name="e">Ignored</param>
        private void SelfPlayDelayTick(object sender, EventArgs e)
        {
            System.Windows.Forms.Timer timer = sender as System.Windows.Forms.Timer;
            if (timer != null)
            {
                chessGame?.StopEngineSelfPlay();
                SelfPlayResultEventArgs spea = new SelfPlayResultEventArgs(true);
                selfPlayToolStripSelfPlay_Click(sender, spea);
                timer.Enabled = false;
            }
        }

        /// <summary>
        /// Event handler fired when a normal play game has finished
        /// </summary>
        /// <param name="sender">Ignored/passed through</param>
        /// <param name="e">Ignored/passed through</param>
        private void ChessGameNormalPlayGameOverEventHandler(object sender, EventArgs e)
        {
            ReportUptime();

            // Probably coming from the non-UI thread the engine is using
            Invoke((MethodInvoker)delegate
            {
                GameResult winner;
                winner = chessGame.GetWinner();
                if (winner == GameResult.Stalemate)
                {
                    MessageBox.Show(this, "Stalemate!", "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, String.Format("Winner: {0}", winner.ToString()), "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
        }

        /// <summary>
        /// Starts a new game if an engine is loaded
        /// </summary>
        private void NewGame(string fen, PieceColor playerColor, int engineThinkTimeInMs)
        {
            if (fullPathToChessExe != null)
            {
                chessGame?.StopEngineSelfPlay();
                
                // Resize the form if needed
                Size gameSize = ChessGame.RequestedSize;
                ClientSize = gameSize;
                textBoxMoveHistory.Location = new Point(ClientSize.Width - ChessBoardView.MoveHistoryWidthInPixels - 25, 75);
                textBoxMoveHistory.Height = ChessBoardView.BoardSizeInPixels / 2;
                textBoxMoveHistory.Width = ChessBoardView.MoveHistoryWidthInPixels;
                textBoxMoveHistory.Font = new Font("Segoe UI", 8);
                textBoxMoveHistory.Text = String.Empty;

                // Old game is dead to us
                if (chessGame != null)
                {
                    chessGame.OnChessGameSelfPlayGameOver -= ChessGameSelfPlayGameOverEventHandler;
                    chessGame.OnChessGameNormalPlayGameOver -= ChessGameNormalPlayGameOverEventHandler;
                }
                chessGame?.Dispose();

                // Now we have the engine path, so create an instance of the game class
                ChessEngineProcessLoader loader = new ChessEngineProcessLoader();
                chessGame = new ChessGame(view, fullPathToChessExe, loader, reduceEngineStrength, Thread.CurrentThread.CurrentCulture);
                chessGame.OnChessGameSelfPlayGameOver += ChessGameSelfPlayGameOverEventHandler;
                chessGame.OnChessGameNormalPlayGameOver += ChessGameNormalPlayGameOverEventHandler;
                if (fen == String.Empty)
                {
                    chessGame.NewGame(playerColor, engineThinkTimeInMs);
                }
                else
                {
                    chessGame.NewPosition(playerColor, fen, engineThinkTimeInMs);
                }

                // Trigger Paint event (draws the initial board)
                Invalidate();
            }
            else
            {
                // No engine loaded, so no new game can be created.  Inform the user
                MessageBox.Show(this, Properties.Resources.ErrorNoEngineLoaded, Properties.Resources.ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Writes uptime to the trace
        /// </summary>
        private void ReportUptime()
        {
            TimeSpan ts = upTime.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Trace.WriteLine("Total Uptime: " + elapsedTime);
        }

        /// <summary>
        /// Called when the form is closing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void APMD_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (chessGame != null)
            {
                chessGame.OnChessGameSelfPlayGameOver -= ChessGameSelfPlayGameOverEventHandler;
                chessGame.OnChessGameNormalPlayGameOver -= ChessGameNormalPlayGameOverEventHandler;
            }
            chessGame?.Quit(); // Quit any current game
            upTime.Stop();
            ((IDisposable)view).Dispose();
        }

        /// <summary>
        /// Attempt to undo the last move made
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void UndoLastMoveToolStripMenuItemUndoLastMove_Click(object sender, EventArgs e)
        {
            chessGame?.UndoLastMove();
            Invalidate();
        }

        /// <summary>
        /// Show the about dialog...
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutDialog dialog = new AboutDialog();
            dialog.ShowDialog(this);
        }

        /// <summary>
        /// Edit->Show FEN handler
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void showFENToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Get the FEN if we have one
            string fen = chessGame?.GetCurrentFEN();
            if (fen != null)
            {
                DisplayFENDialog fenDialog = new DisplayFENDialog(fen);
                fenDialog.ShowDialog();
            }
        }

        /// <summary>
        /// Edit->Options handler
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EngineOptionsDialog optionsDialog = new EngineOptionsDialog(reduceEngineStrength);
            optionsDialog.ShowDialog(this);
            reduceEngineStrength = optionsDialog.ReduceEngineStrength;
        }

        private void APMD_Form_Load(object sender, EventArgs e)
        {
            // The form is only really needed at this point to get the move history
            // Textbox control.  The view is Form based, so hiding it won't gain much
            view = new ChessBoardView(this);
            this.textBoxMoveHistory.Size = new Size(0, 0);
        }
        #endregion

        #region Public Fields
        /// <summary>
        /// Verbose text label control name - used to write verbose output from the
        /// engine, though currently, it only shows a progress string
        /// </summary>
        public static string EngineUpdateControlName = "labelVerbose";

        /// <summary>
        /// Text box on main form for the move history
        /// </summary>
        public static string MoveHistoryControlName = "textBoxMoveHistory";
        #endregion

        #region Private Fields
        private string fullPathToChessExe;
        private ChessGame chessGame;
        private int selfPlayThinkTime;
        private bool reduceEngineStrength;
        private Stopwatch upTime;
        private ChessBoardView view;
        #endregion



        #region Modifications
        /// <summary>
        /// Color of the player (0 for white, 1 for black)
        /// </summary>
        public static bool Color;

        private void SerialRead(object sender, SerialDataReceivedEventArgs e)
        {
            //SerialPort _port = (SerialPort)sender;
            string indata = _port.ReadExisting();
            Debug.WriteLine("Data Received:");
            Debug.Write(indata);
            VirtualClick(indata.Substring(0, 2));
            VirtualClick(indata.Substring(2, 2));
        }

        private void textBoxMoveHistory_TextChanged(object sender, EventArgs e)
        {
            try 
            {
                /*
                ChessEngineProcessLoader loader = new ChessEngineProcessLoader();
                UCIChessEngine engine = new UCIChessEngine(loader);
                engine.OnChessEngineResponseReceived += Handler;
                Debug.WriteLine("-------------------------------------------- Eval ______________________________________");
                UciEvalCommand Eval = new UciEvalCommand();
                Eval.Execute(engine);*/


                //Debug.WriteLine("Olha la ein, olha, mudou ein");
                string lastEMove = textBoxMoveHistory.Text.Substring(textBoxMoveHistory.Text.Length - 6, 4);
                if(lastEMove[0] == '|')
                    lastEMove = textBoxMoveHistory.Text.Substring(textBoxMoveHistory.Text.Length - 4, 4);
                WriteSerial(lastEMove);
                //Debug.WriteLine("-------------------------------------" + lastEMove);
            } catch (Exception E) { E = null; }
        }

        /// <summary>
        /// Send Evluation of the position to the phtsical boad
        /// </summary>
        /// 
        public static void SendEval(string response)
        {
            Debug.WriteLine("---------------------" + response);
            if (response.Substring(16, 7) == "       ")
            {
                Debug.WriteLine("Eval" + response.Substring(23, 5));
                WriteSerial("Eval" + response.Substring(23, 5));
            }
        }

        private static void WriteSerial(string Move)
        {
            try
            {
                _port.WriteLine(Move);
                Console.WriteLine("acabou de enviar serial");
            } catch (NullReferenceException E) { E = null; }
        }

        private void VirtualClick(String move)
        {
            if (Color)
            {
                int X = 25 + 40 + 80 * (7 - (System.Convert.ToInt32(move[0]) - 97));
                int Y = 75 + 40 + 80 * ((System.Convert.ToInt32(move[1]) - 49));
                chessGame?.ProcessClick(X, Y);
                Debug.WriteLine(X + " " + Y + " " + (7 - (System.Convert.ToInt32(move[0]) - 97)) + " " + move[1]);
            }
            else
            {
                int X = 25 + 40 + 80 * (System.Convert.ToInt32(move[0]) - 97);
                int Y = 75 + 40 + 80 * (7 - (System.Convert.ToInt32(move[1]) - 49));
                chessGame?.ProcessClick(X, Y);
                Debug.WriteLine(X + " " + Y + " " + (7 - (System.Convert.ToInt32(move[1]) - 49)) + " " + move[1]);
            }
        }

        private void CkbSerial_Click(object sender, EventArgs e)
        {

            try
            {
                if (CkbSerial.Checked == true)
                {
                    Comports.Enabled = false;
                    _port = new SerialPort((Comports.Items[Comports.SelectedIndex] as string));
                    _port.BaudRate = 9600;
                    _port.StopBits = StopBits.One;
                    _port.Parity = Parity.None;
                    _port.DataBits = 8;
                    _port.DtrEnable = true;
                    _port.Open();
                    _port.DataReceived += new SerialDataReceivedEventHandler(SerialRead);
                }
                else
                {
                    Comports.Enabled = true;
                    //_analyzer.Serial = null;
                    if (_port != null)
                    {
                        _port.Close();
                        _port.Dispose();
                        _port = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }

        }

        private void Comports_DropDownOpened(object sender, EventArgs e)
        {
            Comports.Items.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports) Comports.Items.Add(port);
        }

        #endregion
    }
}

