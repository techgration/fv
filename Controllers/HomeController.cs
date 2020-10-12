using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FilterVids.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace FilterVids.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private string _youtubeDownloadMainFolder = "/app/Views/Home/";
        private string _ffMpegBinFolder = "";
        private string _bashLogPath = "/app/Views/Home/BashLog";
        //we will append to the two folders below to create the specific bash logs for the specific filtered videos
        private string _bashLogFolder = "/app/Views/Home/BashLog/||YOUTUBEID||";
        private string _bashLogFolderFullPath = "/app/Views/Home/BashLog/||YOUTUBEID||/||YOUTUBEID||.log";
        private string _bashLogErrorFullPath = "/app/Views/Home/BashLog/error.log";

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public bool WaitForFile(string fullPath)
        {
            int numTries = 0;
            while (true)
            {
                ++numTries;
                try
                {
                    // Attempt to open the file exclusively.
                    using (FileStream fs = new FileStream(fullPath,
                        FileMode.Open, FileAccess.ReadWrite,
                        FileShare.None, 100))
                    {
                        fs.ReadByte();

                        // If we got this far the file is ready
                        break;
                    }
                }
                catch (Exception ex)
                {

                    if (numTries > 20)
                    {
                        return false;
                    }

                    // Wait for the lock to be released
                    System.Threading.Thread.Sleep(500);
                }
            }

            return true;
        }

        public void AddBashToLog(string cmd)
        {
            DateTime currentDT = DateTime.Now;

            bool fileReady = WaitForFile(_bashLogFolderFullPath);

            if (fileReady)
            {
                System.IO.File.AppendAllText(_bashLogFolderFullPath, "[" + currentDT.ToString("MM/dd/yyyy hh:mm:ss") + "] -- " + cmd + Environment.NewLine);
            }
            else
            {
                System.IO.File.AppendAllText(_bashLogErrorFullPath, "error: Could not write log file " + "[" + currentDT.ToString("MM/dd/yyyy hh:mm:ss") + "] -- " + cmd + Environment.NewLine);
            }
        }

        public string Bash(string cmd)
        {

            string directory = "";
            return Bash(cmd, directory);
        }

        public string Bash(string cmd, string directory)
        {
            //log the cmd
            AddBashToLog(cmd);

            string escapedArgs = cmd.Replace("\"", "\\\"");

            if (String.IsNullOrEmpty(directory))
            {
                Process process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{escapedArgs}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = directory
                    }
                };
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }
            else
            {
                Process process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{escapedArgs}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }


        }



        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult FilterVid()
        {

            //String result = Bash("youtube-dl https://www.youtube.com/watch?v=SjiwQxbol7I");

            //ViewData["Status"] = result;

            return View();
        }

        [HttpPost]
        public IActionResult FilterVid(String youtubeid)
        {
            ViewData["Status"] = "Retrieving YouTube Video";

            _bashLogFolder = _bashLogFolder.Replace("||YOUTUBEID||", youtubeid);
            _bashLogFolderFullPath = _bashLogFolderFullPath.Replace("||YOUTUBEID||", youtubeid);

            if (!System.IO.Directory.Exists(_bashLogFolder))
            {
                System.IO.Directory.CreateDirectory(_bashLogFolder);
            }

            if (!System.IO.Directory.Exists(_bashLogPath))
            {
                System.IO.Directory.CreateDirectory(_bashLogPath);
            }

            if (!System.IO.File.Exists(_bashLogFolderFullPath))
            {
                System.IO.File.Create(_bashLogFolderFullPath);
            }

            if (!System.IO.Directory.Exists("/app/views/home/" + youtubeid))
            {
                System.IO.Directory.CreateDirectory("/app/views/home/" + youtubeid);
            }

            ProcessVideo(youtubeid);

            ViewData["Status"] = "Success";

            return View();
        }

        private void ProcessVideo(string youtubeID)
        {

            string fileName = "";

            //if (String.IsNullOrEmpty(youtubeURL))
            //{
            //    //for testing
            //    youtubeURL = "https://www.youtube.com/watch?v=e1ErwgsaVjg";
            //    fileName = "PeppaPigSwearing";
            //}
            //else
            //{
            fileName = youtubeID + ".mp4";
            string youtubeURL = "https://www.youtube.com/watch?v=" + youtubeID;
            //}

            string newFolder = CreateVideoFolderFromName(youtubeID, fileName);

            string cmd = "";
            string runfrom = "";

            string filePath = _youtubeDownloadMainFolder + youtubeID + "/" + youtubeID + ".mp4";

            //check if the file already exists so we don't have to download again
            if (!DoesFileExist(filePath))
            {
                //try to create the audio
                //cmd = youtubeDownloadMainFolder + "\\youtube-dl.exe --extract-audio --audio-format m4a " + youtubeUrl;

                //string runfrom = youtubeDownloadMainFolder + "";

                //ExecuteCommand(cmd, runfrom);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                //try to DL the video
                cmd = "youtube-dl " + youtubeURL + "  -o " + filePath + " --format mp4 --write-auto-sub --write-thumbnail ";

                //cmd = "load([' " + youtubeURL + "'])" + filePath + " --write-auto-sub --write-thumbnail ";

                runfrom = _youtubeDownloadMainFolder;

                Bash(cmd, runfrom);
            }

            //the video may not come back as mp4... try to get whatever verion it is
            string videoExtension = GetVideoExtension();

            //grab the file we downloaded

            if (System.IO.File.Exists(_youtubeDownloadMainFolder + youtubeID + "/" + youtubeID + ".mkv"))
            {
                Console.WriteLine("MKV processing");
                ProcessVideoExists(youtubeID, ".mkv");
            }

            else if (System.IO.File.Exists(_youtubeDownloadMainFolder + youtubeID + "/" + youtubeID + ".mp4.mkv"))
            {
                Console.WriteLine("MP4.MKV processing");
                ProcessVideoExists(youtubeID, ".mp4.mkv");
            }

            else if (System.IO.File.Exists(_youtubeDownloadMainFolder + youtubeID + "/" + youtubeID + ".mp4"))
            {
                Console.WriteLine("MP4 processing");
                ProcessVideoExists(youtubeID, ".mp4");
            }

            else if (System.IO.File.Exists(_youtubeDownloadMainFolder + youtubeID + "/" + youtubeID + ".mp4.webm"))
            {
                Console.WriteLine("MP4.WEBM processing");
                ProcessVideoExists(youtubeID, ".mp4.webm");
            }
            else
            {
                throw new Exception("Cannot find video file downloaded");
            }
            //SendEmail();
        }

        private void ProcessVideoExists(string fileName, string youtubeURL)
        {
            Console.WriteLine("YouTubeURL is: " + youtubeURL);
            Console.WriteLine("fileName is: " + fileName);

            //string fileName = "";

            //if (String.IsNullOrEmpty(youtubeURL))
            //{
            //    //for testing
            //    youtubeURL = "https://www.youtube.com/watch?v=e1ErwgsaVjg";
            //    fileName = "PeppaPigSwearing";
            //}
            //else
            //{
            //    fileName = youtubeURL;
            //    youtubeURL = "https://www.youtube.com/watch?v=" + youtubeURL;
            //}

            string newFolder = CreateVideoFolderFromName(youtubeURL, fileName);

            string cmd = "";
            string runfrom = "";


            //check if the file already exists so we don't have to download again
            //if (!DoesFileExist(fileName))
            //{
            //try to create the audio
            //cmd = youtubeDownloadMainFolder + "\\youtube-dl.exe --extract-audio --audio-format m4a " + youtubeUrl;

            //string runfrom = youtubeDownloadMainFolder + "";

            //ExecuteCommand(cmd, runfrom);

            //try to DL the video
            //cmd = "youtube-dl.exe " + youtubeUrl + " -o " + fileName + ".mp4 -f (mp4) --write-auto-sub --write-thumbnail ";

            //cmd = "youtube-dl " + youtubeURL + " -o " + fileName + ".mp4 --write-auto-sub --write-thumbnail ";

            //runfrom = _youtubeDownloadMainFolder;

            //Bash(cmd, runfrom);
            //}

            //the video may not come back as mp4... try to get whatever verion it is
            string videoExtension = GetVideoExtension();

            //grab the file we downloaded

            if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mkv"))
            {
                Console.WriteLine("MKV processing");
                ProcessVideoExists2(fileName, ".mkv");
            }

            else if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp4.mkv"))
            {
                Console.WriteLine("MP4.MKV processing");
                ProcessVideoExists2(fileName, ".mp4.mkv");
            }

            else if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp4"))
            {
                Console.WriteLine("ProcessVideoExists2 begins - MP4 processing");
                ProcessVideoExists2(fileName, ".mp4");
            }

            else if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp4.webm"))
            {
                Console.WriteLine("MP4.WEBM processing");
                ProcessVideoExists2(fileName, ".mp4.webm");
            }
            else
            {
                throw new Exception("Cannot find video file downloaded");
            }
            //SendEmail();
        }

        private string GetVideoExtension()
        {
            //TODO: Fix this
            return "mp4";
        }

        private bool DoesFileExist(string fileName)
        {
            return System.IO.File.Exists(fileName);
        }

        private string CreateVideoFolderFromName(string youtubeid, string fileName)
        {
            //string youtubeid = url;
            //youtubeid = youtubeid.Replace("https://", "");
            //youtubeid = youtubeid.Replace("http://", "");
            //youtubeid = youtubeid.Replace("www.youtube.com/", "");


            string returnValue = "";

            //if (url.Contains("watch?v="))
            //{
            //    //get everything after the question mark
            //    string[] urlArr = youtubeid.Split('?');
            //    youtubeid = urlArr[1].Replace("v=", "");
            //    //Directory.CreateDirectory(youtubeDownloadMainFolder + "\\" + youtubeid);
            CreateDirectory(_youtubeDownloadMainFolder + youtubeid);
            returnValue = (_youtubeDownloadMainFolder + youtubeid);
            //}
            //else
            //{
            //    CreateDirectory("/" + youtubeid);
            //    returnValue = ("/" + youtubeid);
            //}

            return returnValue;

        }

        public void ProcessVideoExists2(string fileName, string fileExtension)
        {
            Console.WriteLine("ProcessVideoExists2 - Finding file to process: " + fileName);

            string currentFile = _youtubeDownloadMainFolder + fileName + "/" + fileName + fileExtension;

            Console.WriteLine("ProcessVideoExists2- Finding file to process: " + currentFile);

            try
            {
                FileInfo fi = new FileInfo(currentFile);
                Console.WriteLine("ProcessVideoExists2 - FileInfo populated");

                string audioFile = AudioExtract(fileName, fileExtension);
                Console.WriteLine("ProcessVideoExists2 - AudioFile is " + audioFile);

                Console.WriteLine("ProcessVideoExists2 - Obtaining mute times");

                List<MuteTime> listOfMuteTimes = null;

                if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".en.vtt"))
                {
                    Console.WriteLine("ProcessVideoExists2 - VTT File exists - en.vtt");
                    listOfMuteTimes = ProcessVTTFile(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".en.vtt");

                }
                else if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp4.en.vtt"))
                {
                    Console.WriteLine("ProcessVideoExists2- VTT File exists - mp4.en.vtt");
                    listOfMuteTimes = ProcessVTTFile(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp4.en.vtt");

                }

                Console.WriteLine("listofMuteTimes count is: " + listOfMuteTimes.Count.ToString());

                if (listOfMuteTimes != null && listOfMuteTimes.Count > 0)
                {
                    string[] audioFileArr = audioFile.Split('.');
                    FileInfo fiAudio = new FileInfo(audioFile);

                    MuteAudioSections(fiAudio.Name.Replace(fiAudio.Extension, ""), fiAudio.Extension, fileName, fileExtension, listOfMuteTimes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        public decimal ConvertToSeconds(string t)
        {
            decimal seconds = 0;

            if (t.Any(char.IsDigit))
            {
                Console.WriteLine("Convert to seconds " + t);
                string[] tArray = t.Split(':');


                seconds = seconds + Convert.ToInt32(tArray[0]) * 3600;
                seconds = seconds + Convert.ToInt32(tArray[1]) * 60;
                if (tArray[2].Contains("."))
                {
                    seconds = seconds + Convert.ToDecimal(tArray[2]);
                }
                else
                {
                    seconds = seconds + Convert.ToInt32(tArray[2]);
                }


            }

            return seconds;


        }

        public void MuteAudioSections(string fileNameAudio, string fileNameAudioExtension, string fileNameVideo, string fileExtensionVideo, List<MuteTime> muteTimes)
        {
            string allMuteTimes = "";
            string cmd = "";

            string currentAudioPath = _youtubeDownloadMainFolder + fileNameAudio + "/" + fileNameAudio + fileNameAudioExtension;
            string nextAudioPath = _youtubeDownloadMainFolder + fileNameAudio + "/" + fileNameAudio + "CurrentAudioEdit1" + fileNameAudioExtension;
            string finalAudioEditPath = "";
            string finalFilePath2 = _youtubeDownloadMainFolder + fileNameAudio + "/" + fileNameAudio + "Final_AudioEdit" + fileNameAudioExtension;

            try
            {
                if (muteTimes != null && muteTimes.Count < 100)
                {
                    for (int idx = 0; idx < muteTimes.Count; idx++)
                    {

                        if (allMuteTimes != "")
                        {
                            allMuteTimes += " ";
                        }
                        allMuteTimes += "volume=enable='between(t," + ConvertToSeconds(muteTimes[idx].StartTime) + "," + ConvertToSeconds(muteTimes[idx].EndTime) + ")':volume=0, ";
                    }
                    //trim the last :
                    allMuteTimes = allMuteTimes.Substring(0, allMuteTimes.Length - 2);

                    if (allMuteTimes.EndsWith(","))
                    {
                        allMuteTimes = allMuteTimes.Substring(0, allMuteTimes.Length - 1);
                    }

                    nextAudioPath = GetNextAudioFilePath(currentAudioPath, fileNameAudio, fileNameAudioExtension);

                    CreateAudioFileWithMuteTimes(allMuteTimes, currentAudioPath, nextAudioPath, fileNameAudio, fileNameAudioExtension);

                    currentAudioPath = nextAudioPath;
                    finalAudioEditPath = nextAudioPath;

                    //blank out the allMuteTimes... just wrote it.
                    allMuteTimes = "";


                }
                else
                {
                    if (muteTimes != null)
                    {
                        //every 100 mute times, process, then keep going
                        for (int idx = 0; idx < muteTimes.Count; idx++)
                        {

                            try
                            {
                                if (allMuteTimes != "")
                                {
                                    allMuteTimes += " ";
                                }

                                allMuteTimes += "volume=enable='between(t," + ConvertToSeconds(muteTimes[idx].StartTime) + "," + ConvertToSeconds(muteTimes[idx].EndTime) + ")':volume=0, ";

                                if (idx > 99 && idx % 100 == 0)
                                {
                                    allMuteTimes = allMuteTimes.Substring(0, allMuteTimes.Length - 2);

                                    if (allMuteTimes.EndsWith(","))
                                    {
                                        allMuteTimes = allMuteTimes.Substring(0, allMuteTimes.Length - 1);
                                    }


                                    //this should be the first time we are downloading... so the file should equal this..

                                    nextAudioPath = GetNextAudioFilePath(currentAudioPath, fileNameAudio, fileNameAudioExtension);

                                    CreateAudioFileWithMuteTimes(allMuteTimes, currentAudioPath, nextAudioPath, fileNameAudio, fileNameAudioExtension);

                                    currentAudioPath = nextAudioPath;
                                    finalAudioEditPath = nextAudioPath;

                                    //blank out the allMuteTimes... just wrote it.
                                    allMuteTimes = "";


                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }

                        //create a final write of the audio file
                        if (allMuteTimes != null && allMuteTimes.Length > 2)
                        {

                            //trim the last :
                            allMuteTimes = allMuteTimes.Substring(0, allMuteTimes.Length - 2);

                            if (allMuteTimes.EndsWith(","))
                            {
                                allMuteTimes = allMuteTimes.Substring(0, allMuteTimes.Length - 1);
                            }

                            nextAudioPath = GetNextAudioFilePath(currentAudioPath, fileNameAudio, fileNameAudioExtension);

                            CreateAudioFileWithMuteTimes(allMuteTimes, currentAudioPath, nextAudioPath, fileNameAudio, fileNameAudioExtension);

                            currentAudioPath = nextAudioPath;
                            finalAudioEditPath = nextAudioPath;

                            //blank out the allMuteTimes... just wrote it.
                            allMuteTimes = "";

                        }

                    }

                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                System.IO.File.AppendAllText(_youtubeDownloadMainFolder + "/exception" + DateTime.Now.ToFileTimeUtc().ToString() + ".txt", ex.ToString());
            }

            if (System.IO.File.Exists(fileNameVideo + "/" + fileNameVideo + "FinalFinal.mp4"))
            {
                System.IO.File.Delete(fileNameVideo + "/" + fileNameVideo + "FinalFinal.mp4");
            }

            //merge the updated audio into a finalized mp4
            cmd = "ffmpeg -i " + _youtubeDownloadMainFolder + fileNameVideo + "/" + fileNameVideo + fileExtensionVideo + " -i " + finalAudioEditPath + " -c:v copy -map 0:v:0 -map 1:a:0 " + _youtubeDownloadMainFolder + fileNameVideo + "/" + fileNameVideo + "FinalFinal.mp4 -y";
            Bash(cmd, _youtubeDownloadMainFolder + "");



            //cmd = "cp " + _youtubeDownloadMainFolder + fileNameVideo + "/" + fileNameVideo + "FinalFinal.mp4" + " /app/views/home/" + fileNameVideo + "/" + fileNameVideo + ".mp4";
            //Bash(cmd, _youtubeDownloadMainFolder + "");

        }


        public string AudioExtract(string fileName, string fileExtension)
        {
            //try to extract the audio with ffmpeg
            //i input-video.avi -vn -acodec copy output-audio.aac

            if (System.IO.File.Exists(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp3"))
            {
                System.IO.File.Delete(_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp3");
            }

            string cmd = _ffMpegBinFolder + "ffmpeg -i " + _youtubeDownloadMainFolder + fileName + "/" + fileName + fileExtension + " -q:a 0 -map a " + _youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp3 -y";

            Console.WriteLine("Executing audio extract command");
            Bash(cmd);

            Console.WriteLine("Audio extract complete. File is at: " + _youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp3");
            return (_youtubeDownloadMainFolder + fileName + "/" + fileName + ".mp3");
        }

        public void CreateAudioFileWithMuteTimes(string allMuteTimes, string currentAudioPath, string nextAudioPath, string mainFileName, string mainExtension)
        {

            if (System.IO.File.Exists(nextAudioPath))
            {
                System.IO.File.Delete(nextAudioPath);
            }

            //wrap the mute times in an escaped string
            allMuteTimes = '"' + allMuteTimes + '"';

            string cmd = "ffmpeg -i " + currentAudioPath + " -af " + allMuteTimes + " -y " + nextAudioPath;
            Bash(cmd);

            //copy the file
            //cmd = " cp " + nextAudioPath + " /app/Views/Home/" + mainFileName;
            //Bash(cmd);

        }

        public string GetNextAudioFilePath(string existingAudioFilePath, string mainFileName, string mainFileNameExtension)
        {
            //loops until we get the next audio file name available

            bool fileDoesNotExist = true;
            int fileDoesNotExistCount = 1;

            //assume this file exists until proven otherwise
            string finalFilePath = existingAudioFilePath;

            while (fileDoesNotExist)
            {
                if (System.IO.File.Exists(finalFilePath))
                {

                    fileDoesNotExistCount = fileDoesNotExistCount + 1;
                    finalFilePath = _youtubeDownloadMainFolder + mainFileName + "/" + mainFileName + "CurrentAudioEdit" + fileDoesNotExistCount.ToString() + mainFileNameExtension;

                    // setting this for clarity, even though it's redundant
                    fileDoesNotExist = true;

                }
                else
                {

                    fileDoesNotExist = false;

                }
            }
            return finalFilePath;
        }


        private void CreateDirectory(string s)
        {
            if (!System.IO.Directory.Exists(s))
            {
                System.IO.Directory.CreateDirectory(s);
            }
        }

        public List<MuteTime> ProcessVTTFile(string filePath)
        {

            List<MuteTime> lMuteTimes = new List<MuteTime>();

            Badwords bw = new Badwords();
            string[] badWordsArray = bw.BadwordsList();

            // Read the file and display it line by line.
            System.IO.StreamReader fileR =
               new System.IO.StreamReader(filePath);

            string line = "";

            string startTimeSpan = "";
            string endTimeSpan = "";

            string currentTimeFound = "";


            List<string> listOfTimesWords = new List<string>();
            Boolean listContainsAWord = false;

            while ((line = fileR.ReadLine()) != null)
            {
                //youtube cc have to have the <c> tag or --> to process...
                if (line.Contains("<c>") || line.Contains("-->"))
                {

                    if (line.Contains("-->"))
                    {
                        startTimeSpan = line.Substring(0, line.IndexOf("-->") - 1);
                        endTimeSpan = line.Substring((line.IndexOf("-->") + 4), 12);
                    }

                    else
                    {
                        string[] lineArray = line.Split('<');

                        foreach (string s in lineArray)
                        {
                            int countOfBadWordsInLine = 0;

                            string currentString = s.Replace("<c>", "");
                            currentString = currentString.Replace("/c>", "");
                            currentString = currentString.Replace("c.Color", "");
                            currentString = currentString.Replace("c>", "");
                            currentString = currentString.Replace(">", "");

                            //did we find a bad word? if so, then do a mute entry
                            if ((currentString.ToLower().Contains("fuck") || currentString.ToLower().Contains("shit") || currentString.ToLower().Contains("bitch") || currentString.ToLower().Contains("cunt") || currentString.ToLower().Contains("__")) || (badWordsArray.Any(Regex.Replace(currentString.ToLower().Replace("?", " ").Replace("-", " ").Replace("?", " ").Replace("!", " ").Replace(".", " ").Replace(",", " "), @"[^\w\s]", " ").Trim().Equals)))
                            {
                                countOfBadWordsInLine++;
                                string currentWord = Regex.Replace(currentString.ToLower().Replace("?", " ").Replace("-", " ").Replace("?", " ").Replace("!", " ").Replace(".", " ").Replace(",", " "), @"[^\w\s]", " ").Trim();
                                listOfTimesWords.Add(currentWord);
                                listContainsAWord = true;
                            }
                            else
                            {
                                currentString = currentString.TrimEnd().TrimStart();

                                if (currentString.Contains(":"))
                                {
                                    //did we find a time? store it
                                    try
                                    {
                                        string[] currentStringTS = currentString.Substring(0, 8).Split(':');
                                        TimeSpan t = new TimeSpan(Convert.ToInt32(currentStringTS[0]), Convert.ToInt32(currentStringTS[1]), Convert.ToInt32(currentStringTS[2]));
                                        currentTimeFound = currentString;
                                        listOfTimesWords.Add(currentString);
                                    }
                                    catch
                                    {

                                    }
                                }


                            }

                        }


                    }


                }



            }


            if (listContainsAWord)
            {
                string badword = "";
                string time1 = startTimeSpan;
                string time2 = endTimeSpan;
                //find the position of the word
                for (int x = 0; x < listOfTimesWords.Count; x++)
                {
                    int output = -1;
                    //is the first char a number
                    if (int.TryParse((listOfTimesWords[x])[0].ToString(), out output))
                    {
                        time1 = listOfTimesWords[x];
                    }
                    else
                    {
                        //this is the bad word
                        badword = listOfTimesWords[x];

                        //is this the end of the array
                        if (x == (listOfTimesWords.Count() - 1))
                        {
                            MuteTime mt = new MuteTime();
                            mt.StartTime = time1;
                            mt.EndTime = endTimeSpan;
                            lMuteTimes.Add(mt);
                        }
                        else
                        {
                            //get the next time in the list, then insert into list of mute times
                            time2 = listOfTimesWords[x + 1];

                            MuteTime mt = new MuteTime();
                            mt.StartTime = time1;
                            mt.EndTime = time2;
                            lMuteTimes.Add(mt);

                        }

                    }
                }

            }



            return lMuteTimes;

        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
