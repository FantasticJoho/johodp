using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HttpProxyUI.Converters;

public class MethodColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string method)
        {
            return method switch
            {
                "GET" => new SolidColorBrush(Color.Parse("#28a745")),
                "POST" => new SolidColorBrush(Color.Parse("#007bff")),
                "PUT" => new SolidColorBrush(Color.Parse("#ffc107")),
                "DELETE" => new SolidColorBrush(Color.Parse("#dc3545")),
                "PATCH" => new SolidColorBrush(Color.Parse("#17a2b8")),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int statusCode)
        {
            return statusCode switch
            {
                >= 200 and < 300 => new SolidColorBrush(Color.Parse("#28a745")),
                >= 300 and < 400 => new SolidColorBrush(Color.Parse("#ffc107")),
                >= 400 and < 500 => new SolidColorBrush(Color.Parse("#fd7e14")),
                >= 500 => new SolidColorBrush(Color.Parse("#dc3545")),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        
        if (value is bool isRunning)
        {
            return isRunning 
                ? new SolidColorBrush(Color.Parse("#28a745")) 
                : new SolidColorBrush(Colors.Gray);
        }
        
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int statusCode && statusCode > 0)
        {
            return statusCode switch
            {
                >= 200 and < 300 => new SolidColorBrush(Color.Parse("#d4edda")),
                >= 300 and < 400 => new SolidColorBrush(Color.Parse("#fff3cd")),
                >= 400 and < 500 => new SolidColorBrush(Color.Parse("#f8d7da")),
                >= 500 => new SolidColorBrush(Color.Parse("#f5c6cb")),
                _ => new SolidColorBrush(Colors.White)
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusCodeVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int statusCode && statusCode > 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CountVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count > 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
