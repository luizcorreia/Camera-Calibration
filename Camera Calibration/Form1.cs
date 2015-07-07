using System.Globalization;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SomeCalibrations
{
    public partial class Form1 : Form
    {

        #region Display and aquaring chess board info

        readonly Capture _capture; // capture device
        private Mat _frame = new Mat();
        private readonly Mat _grayFrame = new Mat();
        int _width; //width of chessboard no. squares in width - 1
        int _height; // heght of chess board no. squares in heigth - 1
        private float _squareSize;
        private Size _patternSize;  //size of chess board to be detected
        VectorOfPointF _corners = new VectorOfPointF(); //corners found from chessboard

        Mat[] _rvecs, _tvecs;

        static Mat[] _frameArrayBuffer;
        int _frameBufferSavepoint;
        bool _startFlag;
        private bool _find;

        #endregion

        #region Current mode variables
        public enum Mode
        {
            View,
            CalculatingIntrinsics,
            Calibrated,
            SavingFrames
        }
        Mode _currentMode = Mode.View;
        #endregion

        #region Getting the camera calibration
        MCvPoint3D32f[][] _cornersObjectList;
        PointF[][] _cornersPointsList;
        VectorOfPointF[] _cornersPointsVec;

        readonly Mat _cameraMatrix = new Mat(3, 3, DepthType.Cv64F, 1);
        readonly Mat _distCoeffs = new Mat(8, 1, DepthType.Cv64F, 1);
        #endregion



        public Form1()
        {
            InitializeComponent();

            CvInvoke.UseOpenCL = false;
            //set up cature as normal
            try
            {
                _capture = new Capture();
                _capture.ImageGrabbed += ProcessFrame;
                _capture.Start();
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }

        }

        void ProcessFrame(object sender, EventArgs e)
        {
            _capture.Retrieve(_frame);
            CvInvoke.CvtColor(_frame, _grayFrame, ColorConversion.Bgr2Gray);

            //apply chess board detection
            if (_currentMode == Mode.SavingFrames)
            {
                _find = CvInvoke.FindChessboardCorners(_grayFrame, _patternSize, _corners, CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
                //we use this loop so we can show a colour image rather than a gray:
                if (_find) //chess board found
                {
                    //make mesurments more accurate by using FindCornerSubPixel
                    CvInvoke.CornerSubPix(_grayFrame, _corners, new Size(11, 11), new Size(-1, -1),
                        new MCvTermCriteria(30, 0.1));

                    //if go button has been pressed start aquiring frames else we will just display the points
                    if (_startFlag)
                    {
                        _frameArrayBuffer[_frameBufferSavepoint] = _grayFrame; //store the image
                        _frameBufferSavepoint++; //increase buffer positon

                        //check the state of buffer
                        if (_frameBufferSavepoint == _frameArrayBuffer.Length)
                            _currentMode = Mode.CalculatingIntrinsics; //buffer full
                    }

                    //draw the results
                    CvInvoke.DrawChessboardCorners(_frame, _patternSize, _corners, _find);
                    string msg = string.Format("{0}/{1}", _frameBufferSavepoint + 1, _frameArrayBuffer.Length);

                    int baseLine = 0;
                    var textOrigin = new Point(_frame.Cols - 2 * 120 - 10, _frame.Rows - 2 * baseLine - 10);
                    CvInvoke.PutText(_frame, msg, textOrigin, FontFace.HersheyPlain, 3, new MCvScalar(0, 0, 255), 2);


                    //calibrate the delay bassed on size of buffer
                    //if buffer small you want a big delay if big small delay
                    Thread.Sleep(100); //allow the user to move the board to a different position
                }
                _corners = new VectorOfPointF();
                _find = false;

            }
            if (_currentMode == Mode.CalculatingIntrinsics)
            {
                for (int k = 0; k < _frameArrayBuffer.Length; k++)
                {
                    _cornersPointsVec[k] = new VectorOfPointF();
                    CvInvoke.FindChessboardCorners(_frameArrayBuffer[k], _patternSize, _cornersPointsVec[k], CalibCbType.AdaptiveThresh
                        | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
                    //for accuracy
                    CvInvoke.CornerSubPix(_grayFrame, _cornersPointsVec[k], new Size(11, 11), new Size(-1, -1),
                         new MCvTermCriteria(30, 0.1));

                    //Fill our objects list with the real world mesurments for the intrinsic calculations
                    var objectList = new List<MCvPoint3D32f>();
                    for (int i = 0; i < _height; i++)
                    {
                        for (int j = 0; j < _width; j++)
                        {
                            objectList.Add(new MCvPoint3D32f(j * _squareSize, i * _squareSize, 0.0F));
                        }
                    }

                    //corners_object_list[k] = new MCvPoint3D32f[];
                    _cornersObjectList[k] = objectList.ToArray();
                    _cornersPointsList[k] = _cornersPointsVec[k].ToArray();
                }

                //our error should be as close to 0 as possible

                double error = CvInvoke.CalibrateCamera(_cornersObjectList, _cornersPointsList, _grayFrame.Size,
                     _cameraMatrix, _distCoeffs, CalibType.RationalModel, new MCvTermCriteria(30, 0.1), out _rvecs, out _tvecs);
                MessageBox.Show(@"Intrinsic Calculation Error: " + error.ToString(CultureInfo.InvariantCulture), @"Results", MessageBoxButtons.OK, MessageBoxIcon.Information); //display the results to the user
                _currentMode = Mode.Calibrated;
            }
            if (_currentMode == Mode.Calibrated)
            {
                Sub_PicturBox.Image = _frame;
                Mat outFrame = _frame.Clone();
                CvInvoke.Undistort(_frame, outFrame, _cameraMatrix, _distCoeffs);
                _frame = outFrame.Clone();
            }

            Main_Picturebox.Image = _frame;
        }

        private void Start_BTN_Click(object sender, EventArgs e)
        {
            _frameArrayBuffer = new Mat[(int)numFrameBuffer.Value];
            _cornersObjectList = new MCvPoint3D32f[_frameArrayBuffer.Length][];
            _cornersPointsList = new PointF[_frameArrayBuffer.Length][];
            _cornersPointsVec = new VectorOfPointF[_frameArrayBuffer.Length];
            _frameBufferSavepoint = 0;

            _width = (int)numChessWidth.Value;//9 //width of chessboard no. squares in width - 1
            _height = (int)numChessHeight.Value;//6 // heght of chess board no. squares in heigth - 1
            _squareSize = (float)numSquareSize.Value;
            _patternSize = new Size(_width, _height); //size of chess board to be detected

            //allow the start
            _startFlag = true;
            _currentMode = Mode.SavingFrames;
        }
    }
}
