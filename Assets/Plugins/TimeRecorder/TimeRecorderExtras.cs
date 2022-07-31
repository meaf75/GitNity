using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meaf75.Unity{
    [Serializable]
    public class DateInfo{
        public int date;
        public int timeInSeconds;
    }

    [Serializable]
    public class MonthInfo{
        public int month;
        public List<DateInfo> dates;
    }

    [Serializable]
    public class YearInfo{
        public int year;
        public List<MonthInfo> months;
    }

    [Serializable]
    public class TimeRecorderInfo {
        public List<YearInfo> years;
        /// <summary> Worked time in seconds </summary>
        public long totalRecordedTime;

        public (DateInfo dayInfo, MonthInfo monthInfo, YearInfo yearInfo) VerifyByDatetime(DateTime dateTime) {
            // Save time
            if(years == null){
                years = new List<YearInfo>();
            }

            var yearInfoIdx = years.FindIndex(y => y.year == dateTime.Year);
            var yearInfo = new YearInfo();

            if(yearInfoIdx == -1){
                // Create & setup year
                yearInfo.year = dateTime.Year;
                yearInfo.months = new List<MonthInfo>();
                years.Add(yearInfo);
            } else{
                // Find year
                yearInfo = years[yearInfoIdx];
            }

            // Initialize year months
            if(yearInfo.months == null){
                yearInfo.months = new List<MonthInfo>();
            }

            var monthInfoIdx = yearInfo.months.FindIndex(m => m.month == dateTime.Month);
            var monthInfo = new MonthInfo();

            if(monthInfoIdx == -1){
                // Create & setup month
                monthInfo.month = dateTime.Month;
                monthInfo.dates = new List<DateInfo>();
                yearInfo.months.Add(monthInfo);
            } else{
                // Find moth
                monthInfo = yearInfo.months[monthInfoIdx];
            }

            // Initialize month dates
            if(monthInfo.dates == null){
                monthInfo.dates = new List<DateInfo>();
            }
            
            var dateInfoIdx = monthInfo.dates.FindIndex(m => m.date == dateTime.Day);
            var dateInfo = new DateInfo();

            if(dateInfoIdx == -1){
                // Create & setup day
                dateInfo.date = dateTime.Day;
                dateInfo.timeInSeconds = 0;
                monthInfo.dates.Add(dateInfo);
            } else{
                // Find day
                dateInfo = monthInfo.dates[dateInfoIdx];
            }

            return (dateInfo, monthInfo, yearInfo);
        }
    }

    [Serializable]
    public class TimeTrackerWindowData{
        /// <summary> This variable cannot be serialized by the JsonUtility </summary>
        public DateTime selectedDate;
        public long selectedDateTicks;

        public TimeTrackerWindowData(){
            selectedDate = DateTime.Now;
        }

        public string GetJson(){
            selectedDateTicks = selectedDate.Ticks;
            return JsonUtility.ToJson(this);
        }
    }

    public static class TimeRecorderExtras {
        public const string TIME_RECORDER_REGISTRY = "time_recorder_registry";
        public const string NEXT_SAVE_TIME_PREF = "next_save_time_recorder";

        public const string CORRUPTED_JSON_BACKUP = "corrupted_time_recorder_json_{0}.json";
        public const string TIME_RECORDER_WINDOW_P_PREF = "time_recorder_window_player_pref";

        public const string TIME_RECORDER_PAUSE_P_PREF = "time_recorder_pause";

        public static string GetPauseButtonLabelForState(bool paused) {
            return  paused ? "Resume ▶" : "Pause ▯▯";
        }
        
        public static readonly string CALENDAR_TEMPLATE_PATH = "CalendarTemplate";
        public static readonly string CALENDAR_TEMPLATE_STYLE_PATH = "CalendarTemplateStyle";
        
        public static readonly string DAY_CONTAINER_TEMPLATE_PATH = "DayContainerTemplate";
        public static readonly string DAY_CONTAINER_TEMPLATE_STYLE_PATH = "DayContainerTemplateStyle";

        public static readonly string EDIT = "Edit";
        public static readonly string SAVE = "Save";
        
        /// <summary> Used to have a way to identify day visualElement </summary>
        public static string GetDayNameFormat(int day, int month, int year) {
            return $"{day + 1}-{month}-{year}";
        }
        
        /// <summary> Retreive day,month and year by given day visualElement name identifier </summary>
        public static (int day, int month, int year) GetDateByDayNameFormat(string dayFormat) {
            var parts = dayFormat.Split('-');
            return (int.Parse(parts[0]),int.Parse(parts[1]),int.Parse(parts[2]));
        }
    }

    public class DayCalendarEvent {
        public VisualElement target;
    }

    public static class DayContainerTemplateNames {
        public const string LABEL_DAY = "label-day";
        public const string EDIT_BTN = "edit-btn";
        public const string LABEL_HOURS = "label-hours";
        public const string EDIT_DAY_CONTAINER = "edit-day-container";
        public const string INPUT_EDIT_MINUTES = "input-edit-minutes";
    }
    
    public static class CalendarContainerTemplateNames {
        public const string BTN_PREV_MONTH = "btn-prev-month";
        public const string BTN_NEXT_MONTH = "btn-next-month";
        public const string LABEL_DATE = "label-date";
        public const string TIME_RECORDER_STATE_BTN = "time-recorder-state-btn";
        public const string LABEL_TOTAL_DEV_TIME = "label-total-dev-time";
    }
}

