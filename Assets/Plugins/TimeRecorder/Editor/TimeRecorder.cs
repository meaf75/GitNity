using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Meaf75.Unity{

    [InitializeOnLoad]
    public class TimeRecorder {
        public static TimeRecorder Instance;

        private static DateTime nextSaveTime;
        private static int saveOnMinutes = 5;

        private static DateTime nextRepaint;

        public static TimeRecorderInfo timeRecorderInfo;

        private string registry_json;

        public static bool isPaused;

        static TimeRecorder(){

            isPaused = PlayerPrefs.GetInt(TimeRecorderExtras.TIME_RECORDER_PAUSE_P_PREF,0) == 1 ;

            // Restore callbacks
            EditorApplication.update += TimeRecorderUpdate;
            EditorApplication.quitting += () => SaveTimeRecorded(false);
        }

        /// <summary> Update plugin in unity editor update event </summary>
        private static void TimeRecorderUpdate(){

            if (isPaused)
                return;

            // Initialize to start Time recorder
            InitializeSaveTime();

            // Do nothing if nextSaveTime not setted for some reason
            if(nextSaveTime == DateTime.MinValue){
                Debug.Log("Need start date");
                return;
            }

            if(nextSaveTime < DateTime.Now){

                if(EditorApplication.timeSinceStartup < saveOnMinutes * 60){    // minutes to secods, timeSinceStartup is stored in seconds
//                    Debug.Log("////////// No sr no voy a guardar ////////////");

                    // If true probably Unity Editor started recently
                    ReCalculateNextSave();
                    return;
                }

                // Save time worked
                bool saved = SaveTimeRecorded(true);

                // Get new nextRefresh
                if(saved)
                    ReCalculateNextSave();
            }
        }

        private static bool SaveTimeRecorded(bool countdownCompleted){
            string timeRecorderJson = PlayerPrefs.GetString(TimeRecorderExtras.TIME_RECORDER_REGISTRY, "");

            // parse stored json data
            try{
                // Is a new time recorder?
                if(string.IsNullOrEmpty(timeRecorderJson)){
                    timeRecorderInfo = InitializeTimeRecorder();
                } else{
                    timeRecorderInfo = JsonUtility.FromJson<TimeRecorderInfo>(timeRecorderJson);
                }

            } catch(Exception e){
                Debug.LogError("Any error ocurred trying to parse TimeRecorder JSON, a json file backup will be generated & data will be refreshed: "+e);

                PlayerPrefs.DeleteKey(TimeRecorderExtras.TIME_RECORDER_REGISTRY);
                PlayerPrefs.Save();

                // Save local backgup
                string fileName = string.Format(TimeRecorderExtras.CORRUPTED_JSON_BACKUP, DateTime.Now.Ticks);
                string path = Path.Combine(Application.dataPath, fileName);
                Task saveBackup = WriteTextAsync(path, timeRecorderJson);

                // Save Data into the next iteration
                return false;
            }

            #region Validate timeRecorderInfo object
            var currentTime = DateTime.Now;
            var dateTimeInfo = timeRecorderInfo.VerifyByDatetime(currentTime);
            #endregion

            int secondsToAdd = saveOnMinutes * 60;

            if(countdownCompleted){
                // Add time reference for current date
                dateTimeInfo.dayInfo.timeInSeconds += secondsToAdd;
            } else{
                if(EditorApplication.timeSinceStartup < saveOnMinutes * 60){
                    // Editor recently oppened so i should save EditorApplication.timeSinceStartup
                    secondsToAdd = (int) EditorApplication.timeSinceStartup;
                } else {
                    // Save time elapsed from the last save time
                    DateTime lastSave = nextSaveTime.AddMinutes(-saveOnMinutes);

                    var diferencia = DateTime.Now - lastSave;
                    secondsToAdd = diferencia.Seconds;
                }
                Debug.Log("No se completó pero aún así guardo");
            }


            timeRecorderInfo.totalRecordedTime += secondsToAdd;

            // Save the registry
            SaveTimeRecorded(timeRecorderInfo);

            Debug.Log("Your develop time has been tracked");
            return true;
        }

        public static void SaveTimeRecorded(TimeRecorderInfo info) {
            // Save the registry
            PlayerPrefs.SetString(TimeRecorderExtras.TIME_RECORDER_REGISTRY, JsonUtility.ToJson(info));
            PlayerPrefs.Save();

            if(TimeRecorderWindow.Instance)
                TimeRecorderWindow.Instance.RepaintWindow();
        }
        
        private static TimeRecorderInfo InitializeTimeRecorder(){

            Debug.Log("Inicializando time recorder");

            DateTime dateTime = DateTime.Now;

            timeRecorderInfo = new TimeRecorderInfo(){
                years =  new List<YearInfo>(),
                totalRecordedTime = 0
            };

            // Setup current year
            var year = new YearInfo(){
                year = dateTime.Year,
                months = new List<MonthInfo>()
            };

            // Setup current month
            var month = new MonthInfo(){
                month = dateTime.Month,
                dates = new List<DateInfo>()
            };

            // Setup current day
            var day = new DateInfo(){
                date = dateTime.Day,
                timeInSeconds = 0,
            };

            // Setup time recorder object
            month.dates.Add(day);
            year.months.Add(month);
            timeRecorderInfo.years.Add(year);

            // Save the registry
            string timeRecorderJson = JsonUtility.ToJson(timeRecorderInfo);

            PlayerPrefs.SetString(TimeRecorderExtras.TIME_RECORDER_REGISTRY, timeRecorderJson);
            PlayerPrefs.Save();

            Debug.Log("Time recorder initialized");

            return timeRecorderInfo;
        }

        /// <summary> Setup time recorder, get save time </summary>
        private static void InitializeSaveTime(){
            // Do nothing if nextSaveTime is already setted
            if(nextSaveTime != DateTime.MinValue){
                return;
            }

            // Get reference to the next datetime ticks to save
            string textNextSave = PlayerPrefs.GetString(TimeRecorderExtras.NEXT_SAVE_TIME_PREF, "");

            if(string.IsNullOrEmpty(textNextSave)){
                // Refresh save time
                textNextSave = ReCalculateNextSave();
            }


            long.TryParse(textNextSave, out var saveTimeTicks);
            nextSaveTime = new DateTime(saveTimeTicks);
        }

        /// <summary> Calculate new save time </summary>
        public static string ReCalculateNextSave(){
            // Create new reference of the next save time
            nextSaveTime = DateTime.Now;
            nextSaveTime = nextSaveTime.AddSeconds(saveOnMinutes * 60);

            // I cannot save ticks as setInt into my player pref
            string textNextSave = nextSaveTime.Ticks.ToString();

            PlayerPrefs.SetString(TimeRecorderExtras.NEXT_SAVE_TIME_PREF, textNextSave);
            PlayerPrefs.Save();

            return textNextSave;
        }

        /// <summary> Returns TimeRecorderInfo based on the registry JSON data </summary>
        public static TimeRecorderInfo LoadTimeRecorderInfoFromRegistry(){
            string timeRecorderJson = PlayerPrefs.GetString(TimeRecorderExtras.TIME_RECORDER_REGISTRY, "");

            try{
                // Get data
                var timeRecorderInfoData = JsonUtility.FromJson<TimeRecorderInfo>(timeRecorderJson);
                return timeRecorderInfoData;
            } catch(Exception){
                Debug.LogError("Your time recorder registry JSON data is corrupted, please restart the Unity Editor");
            }

            // Return cached timeRecorderInfo data
            return timeRecorderInfo;
        }

        private static async Task WriteTextAsync(string filePath, string text){
            byte[] encodedText = Encoding.Unicode.GetBytes(text);

            using(FileStream sourceStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true)){
                await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
                Debug.Log("File saved in: "+ filePath);
            }
        }
    }
}
