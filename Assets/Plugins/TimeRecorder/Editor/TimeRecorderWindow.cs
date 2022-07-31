using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meaf75.Unity{

    public class TimeRecorderWindow : EditorWindow, IHasCustomMenu{

        private static DateTime nextRepaint;

        private static DateTime selectedDate;

        public static TimeRecorderWindow Instance;

        private static TimeRecorderInfo info;
		
		private static readonly Vector2 windowSize = new Vector2(563,560);

        private VisualElement currentDayEditing;

        private const int MINUTES_PER_DAY = 1_440;
        private const int SECONDS_PER_MINUTE = 60;

        [MenuItem("Tools/Time recorder/Time Calendar")]
        static void Init(){

            var runningStateLabel = TimeRecorder.isPaused ? "Paused" : "Running";

            // Get existing open window or if none, make a new one:
            var window = GetWindow<TimeRecorderWindow>();
            window.titleContent = new GUIContent($"Time recorder ({runningStateLabel})");
			
			window.minSize = windowSize;
            window.maxSize = windowSize;
            
            selectedDate = DateTime.Now;
        }

        private void OnEnable(){
            Instance = this;
            selectedDate = DateTime.Now;
            DrawWindow();
        }

        // This interface implementation is automatically called by Unity.
        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
            GUIContent content = new GUIContent("Repaint");
            menu.AddItem(content, false, RepaintWindow);

            GUIContent content2 = new GUIContent("RepaintOnlyFew");
            menu.AddItem(content2, false, RepaintOnlyFew);
        }

        void RepaintOnlyFew(){
            VisualElement root = rootVisualElement;
            var totalDevLabel = root.Q<Label>("label-total-dev-time");
            totalDevLabel.text = "Test repaint";

            Debug.Log("seted");
        }

        private void DrawWindow(){

            VisualElement root = rootVisualElement;
            info = TimeRecorder.LoadTimeRecorderInfoFromRegistry();
            
            var timeRecorderTemplate = Resources.Load<VisualTreeAsset>(TimeRecorderExtras.CALENDAR_TEMPLATE_PATH);
            var timeRecorderTemplateStyle = Resources.Load<StyleSheet>(TimeRecorderExtras.CALENDAR_TEMPLATE_STYLE_PATH);
            root.styleSheets.Add(timeRecorderTemplateStyle);

            var dayElementTemplateStyle = Resources.Load<StyleSheet>(TimeRecorderExtras.DAY_CONTAINER_TEMPLATE_STYLE_PATH);
            root.styleSheets.Add(dayElementTemplateStyle);

            // Add tree to root element
            timeRecorderTemplate.CloneTree(root);

            // Update date label
            var dateLabel = root.Q<Label>(CalendarContainerTemplateNames.LABEL_DATE);
            dateLabel.text = $"{selectedDate.Day}-{selectedDate.Month}-{selectedDate.Year}";

            // Fix buttons action
            var prevMonthBtn = root.Q<Button>( CalendarContainerTemplateNames.BTN_PREV_MONTH);
            var nextMonthBtn = root.Q<Button>(CalendarContainerTemplateNames.BTN_NEXT_MONTH);
            var timeRecorderPauseStateBtn = root.Q<Button>( CalendarContainerTemplateNames.TIME_RECORDER_STATE_BTN);

            prevMonthBtn.clicked += () => ChangeMonthOffset(-1);
            nextMonthBtn.clicked += () => ChangeMonthOffset(1);
            timeRecorderPauseStateBtn.clicked += ChangeTimeRecorderPauseState;

            timeRecorderPauseStateBtn.text = TimeRecorderExtras.GetPauseButtonLabelForState(TimeRecorder.isPaused).ToUpperInvariant();

            // Set total label dev time
            var totalDevLabel = root.Q<Label>( CalendarContainerTemplateNames.LABEL_TOTAL_DEV_TIME);
            totalDevLabel.text = GetLabel(info?.totalRecordedTime ?? 0);

            // Generate days
            var daysContainers = new VisualElement[7];

            // Get reference to all containers before generate elements
            for(int i = 0; i < 7; i++){
                var dayElement = root.Q<VisualElement>("day-"+i);
                daysContainers[i] = dayElement;
            }

            int daysGenerated = 0;

            #region Step 1: Fill previous month
            var firstDay = new DateTime(selectedDate.Year,selectedDate.Month,1);

            if(firstDay.DayOfWeek != DayOfWeek.Monday){
                // Get previous month
                var previousMonthDate = selectedDate.AddMonths(-1);
                previousMonthDate = new DateTime(previousMonthDate.Year,previousMonthDate.Month,1);
                int end = DateTime.DaysInMonth(previousMonthDate.Year, previousMonthDate.Month);
                int reduceDays = firstDay.DayOfWeek == DayOfWeek.Sunday ? 6 : ((int) firstDay.DayOfWeek - 1) % 7;
                int start = end - reduceDays;

                // Generate Elements
                FillDateToDate(daysContainers,previousMonthDate,start,end,ref daysGenerated,true);
            }
            #endregion

            #region Step 2: Fill selected month
                int daysInSelectedMonth = DateTime.DaysInMonth(selectedDate.Year, selectedDate.Month);

                FillDateToDate(daysContainers,selectedDate,0,daysInSelectedMonth,ref daysGenerated);
            #endregion

            #region Step 3: Fill remaining month days
                var selectedMonthFinalDate = new DateTime(selectedDate.Year, selectedDate.Month, daysInSelectedMonth);

                if(selectedMonthFinalDate.DayOfWeek != DayOfWeek.Sunday){
                    // In this calendar the final day is "Sunday"
                    // so if selected month ends in "Sunday" there is no need to fill remaining days

                    var nextMonth = selectedDate.AddMonths(1);
                    int remainingDays = 7 - ((int) selectedMonthFinalDate.DayOfWeek);

                    FillDateToDate(daysContainers,nextMonth,0,remainingDays, ref daysGenerated,true);
                }
            #endregion
        }

        void FillDateToDate(VisualElement[] daysContainers,DateTime dateSelected, int start, int end, ref int daysGenerated, bool emptyMode = false){

            var dayElementTemplate = Resources.Load<VisualTreeAsset>("DayContainerTemplate");
            
            var displayingYear = info?.years?.Find(y => y.year == selectedDate.Year);
            var displayingMonth = displayingYear?.months.Find(m => m.month == selectedDate.Month);

            // Generate Date elements
            for(int i = start; i < end; i++){
                var date = new DateTime(dateSelected.Year, dateSelected.Month, i + 1);

                var dayContainer = daysContainers[(int) date.DayOfWeek];

                var dayElement = dayElementTemplate.CloneTree();
                dayElement.name = TimeRecorderExtras.GetDayNameFormat(i,dateSelected.Month,dateSelected.Year);
                
                string hoursTxt = "";
                    
                // Control when the user focus a valid day
                if (!emptyMode) {
                    
                    var focusedElement = new DayCalendarEvent { target = dayElement };
                    var editBtn = dayElement.Q<Button>(DayContainerTemplateNames.EDIT_BTN);
                    var inputEditMinutes = dayElement.Q<TextField>(DayContainerTemplateNames.INPUT_EDIT_MINUTES);
                    
                    dayElement.RegisterCallback<MouseEnterEvent,DayCalendarEvent>(OnMouseEnterDayContainer, focusedElement);
                    dayElement.RegisterCallback<MouseLeaveEvent,DayCalendarEvent>(OnMouseExitDayContainer, focusedElement);
                    inputEditMinutes.RegisterCallback<ChangeEvent<string>, DayCalendarEvent>(OnChangeValueEditMinutesInput,focusedElement);
                    
                    
                    // Add events to handle this element
                    editBtn.clicked += () => OnPressEditBtn(dayElement);
                }

                // Fill seleted month with sotred data
                if(displayingMonth != null && !emptyMode){
                    // Get & set worked day info
                    var dayInfoIdx = displayingMonth.dates.FindIndex(d => d.date == i + 1);

                    if(dayInfoIdx != -1){
                        // Set worked time
                        var dayInfo = displayingMonth.dates[dayInfoIdx];

                        hoursTxt = GetLabel(dayInfo.timeInSeconds);
                    }
                }

                var dayLabel = dayElement.Q<Label>( DayContainerTemplateNames.LABEL_DAY);
                dayLabel.text = emptyMode ? "" : $"{i+1}";

                var hoursLabel = dayElement.Q<Label>( DayContainerTemplateNames.LABEL_HOURS);
                hoursLabel.text = hoursTxt;

                dayContainer.Add(dayElement);

                daysGenerated++;
            }
        }

        #region Calendar events events
        private void OnMouseEnterDayContainer(MouseEnterEvent mouseEvent, DayCalendarEvent e) {
            var editBtn = e.target.Q<Button>(DayContainerTemplateNames.EDIT_BTN);
            ChangeDayEditState(e.target, true, editBtn);
        }
        
        private void OnMouseExitDayContainer(MouseLeaveEvent mouseEvent, DayCalendarEvent e) {
            var editBtn = e.target.Q<Button>(DayContainerTemplateNames.EDIT_BTN);

            if (currentDayEditing != e.target) // Hide edit btn if is not editing this day
                ChangeDayEditState(e.target, false, editBtn);
        }
        
        private void OnChangeValueEditMinutesInput(ChangeEvent<string> changeEvt, DayCalendarEvent e) {
            var inputEditMinutes = e.target.Q<TextField>(DayContainerTemplateNames.INPUT_EDIT_MINUTES);

            // Fix input value if is needed
            if (ClampDayMinutesValue(changeEvt.previousValue, changeEvt.newValue, out var fixedValue)) {
                inputEditMinutes.value = fixedValue.ToString();
            }
        }

        private bool ClampDayMinutesValue(string previousValue, string newValue, out int outValue) {
            if (string.IsNullOrEmpty(newValue)) {
                outValue = 0;
                return false;
            }
            
            if (!int.TryParse(newValue, out var value)) {
                // input is not a number, THIS WILL TRIGGER THE CALLBACK AGAIN
                outValue = int.Parse(previousValue);
                return true;
            }
            
            // Only positive numbers
            if (value < 0) {
                outValue = Mathf.Abs(value);
                return true;
            }

            // If the new value can be changed/fix then do it
            // Example "000" can be fixed as "0"
            if (value.ToString() != newValue) {
                // THIS WILL TRIGGER THE CALLBACK AGAIN
                outValue = value;
                return true;
            }

            // Fix max minutes per day
            if (value > MINUTES_PER_DAY) {
                // THIS WILL TRIGGER THE CALLBACK AGAIN
                Debug.Log($"A day only contains {MINUTES_PER_DAY} minutes");
                outValue = MINUTES_PER_DAY;
                return true;
            }
            
            outValue = value;
            return false;
        }

        private void OnPressEditBtn(VisualElement dayElement) {

            if (info == null) {
                // Can be null the first time
                info = new TimeRecorderInfo();
            }
            
            var editContainer = dayElement.Q<VisualElement>(DayContainerTemplateNames.EDIT_DAY_CONTAINER);
            var editBtn = dayElement.Q<Button>(DayContainerTemplateNames.EDIT_BTN);
            var labelHours = dayElement.Q<Label>(DayContainerTemplateNames.LABEL_HOURS);
            var textField = dayElement.Q<TextField>(DayContainerTemplateNames.INPUT_EDIT_MINUTES);
            var date = TimeRecorderExtras.GetDateByDayNameFormat(dayElement.name);

            var dateTime = new DateTime(date.year, date.month, date.day); 
            var dateTimeInfo = info.VerifyByDatetime(dateTime);
            
            // User is editing this day?
            if (currentDayEditing == dayElement) {  // If true this btn is working as save
                // ◘◘◘◘◘ On press Save ◘◘◘◘◘
                var totalDevLabel = rootVisualElement.Q<Label>( CalendarContainerTemplateNames.LABEL_TOTAL_DEV_TIME);
                
                editBtn.text = TimeRecorderExtras.EDIT;

                currentDayEditing = null;
                editContainer.AddToClassList("no-display-element");
                labelHours.RemoveFromClassList("no-display-element");

                ClampDayMinutesValue(textField.value, textField.value, out var time);

                // ⚠⚠⚠⚠⚠ Transform input minutes to seconds ⚠⚠⚠⚠⚠
                time = time * SECONDS_PER_MINUTE;
                
                info.totalRecordedTime = info.totalRecordedTime - dateTimeInfo.dayInfo.timeInSeconds + time;    // Update total time
                dateTimeInfo.dayInfo.timeInSeconds = time;  // Update day time

                labelHours.text = GetLabel(time);
                totalDevLabel.text = GetLabel(info.totalRecordedTime);
                
                TimeRecorder.SaveTimeRecorded(info);
                TimeRecorder.ReCalculateNextSave();

                Debug.Log($"<color=green>Time of the day {dayElement.name} updated</color>");
                
                return;
            }

            if (currentDayEditing != null) {
                // Hide previous editing button
                currentDayEditing.Q<Label>(DayContainerTemplateNames.LABEL_HOURS).RemoveFromClassList("no-display-element");
                ChangeDayEditState(currentDayEditing, false);
            }
            
            // Set the info of selected date
            textField.value = (dateTimeInfo.dayInfo.timeInSeconds / SECONDS_PER_MINUTE).ToString();
            
            editContainer.RemoveFromClassList("no-display-element");
            labelHours.AddToClassList("no-display-element");
            
            currentDayEditing = dayElement;
            editBtn.text = TimeRecorderExtras.SAVE;
        }
        
        private void ChangeDayEditState(VisualElement dayElement, bool active, Button btn = null) {

            if(btn == null)
                btn = dayElement.Q<Button>(DayContainerTemplateNames.EDIT_BTN);

            if (active) {
                btn.RemoveFromClassList("no-display-element");
            } else {
                btn.AddToClassList("no-display-element");
                dayElement.Q<VisualElement>(DayContainerTemplateNames.EDIT_DAY_CONTAINER).AddToClassList("no-display-element");
                
                btn.text = TimeRecorderExtras.EDIT;
            }
        }
        #endregion
        
        /// <summary> Change month  </summary>
        /// <param name="offset">-1 or 1</param>
        void ChangeMonthOffset(int offset) {
            currentDayEditing = null;
            selectedDate = selectedDate.AddMonths(offset);
            RepaintWindow();
        }

        public void RepaintWindow(){
//            Debug.Log("Repainteando ");
            VisualElement root = rootVisualElement;
            root.Clear();
            DrawWindow();
        }

        string GetLabel(long timeInSeconds){

            var timespan = TimeSpan.FromSeconds(timeInSeconds);

            // Check if worked time is less than a second
            if (timespan.TotalSeconds < 60) {

                if ((int) timespan.TotalSeconds <= 0) {
                    return "";
                }
                
                return (int) timespan.TotalSeconds + " sec";
            }

            // Check if worked time is less than an hour
            if(timespan.TotalMinutes < 60)
                return (int) timespan.TotalMinutes + " min";

            string label = $"{(int) timespan.TotalHours} h";

            if (timespan.Minutes > 0) {
                label += $"\n{timespan.Minutes} min";
            }
            
            return label;
        }

        private void ChangeTimeRecorderPauseState() {
            TimeRecorderTools.ChangeTimeRecorderPauseState(!TimeRecorder.isPaused);
        }

        /// <summary> Update visual elements from this window </summary>
        /// <param name="paused">is time recorder paused?</param>
        public void UpdatePausedState(bool paused) {
            TimeRecorder.isPaused = paused;

            var runningStateLabel = TimeRecorder.isPaused ? "Paused" : "Running";

            var timeRecorderPauseStateBtn = rootVisualElement.Q<Button>("time-recorder-state-btn");
            timeRecorderPauseStateBtn.text = TimeRecorderExtras.GetPauseButtonLabelForState(TimeRecorder.isPaused).ToUpperInvariant();

            GetWindow<TimeRecorderWindow>().titleContent = new GUIContent($"Time recorder ({runningStateLabel})");
        }
    }
}
