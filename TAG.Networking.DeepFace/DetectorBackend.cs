namespace TAG.Networking.DeepFace
{
	/// <summary>
	/// DeepFace detector backends
	/// </summary>
	public enum DetectorBackend
	{
		OpenCV = 0, 
		Ssd = 1, 
		Dlib = 2, 
		Mtcnn = 3, 
		FastMtcnn = 4,
	    RetinaFace = 5, 
		MediaPipe = 6, 
		Yolov8n = 7, 
		Yolov8m = 8, 
	    Yolov8l = 9, 
		Yolov11n = 10, 
		Yolov11s = 11, 
		Yolov11m = 12,
	    Yolov11l = 13, 
		Yolov12n = 14, 
		Yolov12s = 15, 
		Yolov12m = 16,
	    Yolov12l = 17, 
		Yunet = 18, 
		CenterFace = 19
	}
}
