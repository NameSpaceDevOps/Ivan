using Ivan.Domain.Enums;

namespace Ivan.Web.Services;

public class UserPreferencesState
{
    public event Action? OnChange;

    public string Culture { get; private set; } = "fa-IR";
    public CalendarType Calendar { get; private set; } = CalendarType.Jalali;
    public DigitStyle DigitStyle { get; private set; } = DigitStyle.Persian;
    public ThemePreference Theme { get; private set; } = ThemePreference.Light;
    public string ActivePersona { get; private set; } = "Parent"; // Parent, Teen, Child

    public bool IsRtl => Culture.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ||
                         Culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    public void SetCulture(string culture)
    {
        if (Culture != culture)
        {
            Culture = culture;
            if (culture.StartsWith("fa", StringComparison.OrdinalIgnoreCase))
            {
                Calendar = CalendarType.Jalali;
                DigitStyle = DigitStyle.Persian;
            }
            else
            {
                Calendar = CalendarType.Gregorian;
                DigitStyle = DigitStyle.Latin;
            }
            NotifyStateChanged();
        }
    }

    public void SetCalendar(CalendarType calendar)
    {
        if (Calendar != calendar)
        {
            Calendar = calendar;
            NotifyStateChanged();
        }
    }

    public void SetDigitStyle(DigitStyle digitStyle)
    {
        if (DigitStyle != digitStyle)
        {
            DigitStyle = digitStyle;
            NotifyStateChanged();
        }
    }

    public void SetPersona(string persona)
    {
        if (ActivePersona != persona)
        {
            ActivePersona = persona;
            NotifyStateChanged();
        }
    }

    public void ToggleRtlLtr()
    {
        SetCulture(IsRtl ? "en-US" : "fa-IR");
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
