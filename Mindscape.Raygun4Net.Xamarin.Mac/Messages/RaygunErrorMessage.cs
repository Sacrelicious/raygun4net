﻿using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Text;
using System.Collections.Generic;

namespace Mindscape.Raygun4Net.Messages
{
  public class RaygunErrorMessage
  {
    public RaygunErrorMessage ()
    {
    }

    public RaygunErrorMessage(Exception exception)
    {
      var exceptionType = exception.GetType();

      Message = string.Format("{0}: {1}", exceptionType.Name, exception.Message);
      ClassName = exceptionType.FullName;

      StackTrace = BuildStackTrace(exception);
      Data = exception.Data;

      if (exception.InnerException != null)
      {
        InnerError = new RaygunErrorMessage(exception.InnerException);
      }
    }

    private RaygunErrorStackTraceLineMessage[] BuildStackTrace(Exception exception)
    {
      var lines = new List<RaygunErrorStackTraceLineMessage>();

      string stackTraceStr = exception.StackTrace;
      if (stackTraceStr == null)
      {
        var line = new RaygunErrorStackTraceLineMessage { FileName = "none", LineNumber = 0 };
        lines.Add(line);
        return lines.ToArray();
      }

      try
      {
        RaygunErrorStackTraceLineMessage[] array = ParseStackTrace(exception.StackTrace);
        if (array.Length > 0)
        {
          return array;
        }
      }
      catch { }



      var stackTrace = new StackTrace(exception, true);
      var frames = stackTrace.GetFrames();

      if (frames == null || frames.Length == 0)
      {
        var line = new RaygunErrorStackTraceLineMessage { FileName = "none", LineNumber = 0 };
        lines.Add(line);
        return lines.ToArray();
      }

      foreach (StackFrame frame in frames)
      {
        MethodBase method = frame.GetMethod();

        if (method != null)
        {
          int lineNumber = frame.GetFileLineNumber();

          if (lineNumber == 0)
          {
            lineNumber = frame.GetILOffset();
          }

          var methodName = GenerateMethodName(method);

          string file = frame.GetFileName();

          string className = method.ReflectedType != null
            ? method.ReflectedType.FullName
            : "(unknown)";

          var line = new RaygunErrorStackTraceLineMessage
          {
            FileName = file,
            LineNumber = lineNumber,
            MethodName = methodName,
            ClassName = className
          };

          lines.Add(line);
        }
      }

      return lines.ToArray();
    }

    protected RaygunErrorStackTraceLineMessage[] ParseStackTrace(string stackTrace)
    {
      var lines = new List<RaygunErrorStackTraceLineMessage>();

      string[] stackTraceLines = stackTrace.Split(new char[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
      foreach (string stackTraceLine in stackTraceLines)
      {
        int lineNumber = 0;
        string fileName = null;
        string methodName = null;
        string className = null;
        string stackTraceLn = stackTraceLine;
        // Line number
        int index = stackTraceLine.LastIndexOf(":");
        if (index > 0)
        {
          bool success = int.TryParse(stackTraceLn.Substring(index + 1), out lineNumber);
          if (success)
          {
            stackTraceLn = stackTraceLn.Substring(0, index);
          }
        }
        // File name
        index = stackTraceLn.LastIndexOf("] in ");
        if (index > 0)
        {
          fileName = stackTraceLn.Substring(index + 5);
          stackTraceLn = stackTraceLn.Substring(0, index);
        }
        // Method name
        index = stackTraceLn.LastIndexOf("(");
        if (index > 0 && !stackTraceLine.StartsWith("at ("))
        {
          index = stackTraceLn.LastIndexOf(".", index);
          if (index > 0)
          {
            int endIndex = stackTraceLn.IndexOf("[0x");
            if (endIndex < 0)
            {
              endIndex = stackTraceLn.Length;
            }
            methodName = stackTraceLn.Substring(index + 1, endIndex - index - 1).Trim();
            methodName = methodName.Replace(" (", "(");
            stackTraceLn = stackTraceLn.Substring(0, index);

            // Memory address
            index = methodName.LastIndexOf("<0x");
            if (index >= 0)
            {
              fileName = methodName.Substring(index);
              methodName = methodName.Substring(0, index).Trim();
            }

            // Class name
            index = stackTraceLn.IndexOf("at ");
            if (index >= 0)
            {
              className = stackTraceLn.Substring(index + 3);
            }
          }
        }

        if (methodName == null && fileName == null)
        {
          if (!String.IsNullOrWhiteSpace(stackTraceLn) && stackTraceLn.StartsWith("at "))
          {
            stackTraceLn = stackTraceLn.Substring(3);
          }
          fileName = stackTraceLn;
        }

        if ("<filename unknown>".Equals(fileName))
        {
          fileName = null;
        }

        var line = new RaygunErrorStackTraceLineMessage
        {
          FileName = fileName,
          LineNumber = lineNumber,
          MethodName = methodName,
          ClassName = className
        };

        lines.Add(line);
      }

      return lines.ToArray();
    }

    private string GenerateMethodName(MethodBase method)
    {
      var stringBuilder = new StringBuilder();

      stringBuilder.Append(method.Name);

      if (method is MethodInfo && method.IsGenericMethod)
      {
        Type[] genericArguments = method.GetGenericArguments();
        stringBuilder.Append("[");
        int index2 = 0;
        bool flag2 = true;
        for (; index2 < genericArguments.Length; ++index2)
        {
          if (!flag2)
            stringBuilder.Append(",");
          else
            flag2 = false;
          stringBuilder.Append(genericArguments[index2].Name);
        }
        stringBuilder.Append("]");
      }
      stringBuilder.Append("(");
      ParameterInfo[] parameters = method.GetParameters();
      bool flag3 = true;
      for (int index2 = 0; index2 < parameters.Length; ++index2)
      {
        if (!flag3)
          stringBuilder.Append(", ");
        else
          flag3 = false;
        string str2 = "<UnknownType>";
        if (parameters[index2].ParameterType != null)
          str2 = parameters[index2].ParameterType.Name;
        stringBuilder.Append(str2 + " " + parameters[index2].Name);
      }
      stringBuilder.Append(")");

      return stringBuilder.ToString();
    }

    public RaygunErrorMessage InnerError { get; set; }

    public IDictionary Data { get; set; }

    public string ClassName { get; set; }

    public string Message { get; set; }

    public RaygunErrorStackTraceLineMessage[] StackTrace { get; set; }

    public override string ToString()
    {
      // This exists because Reflection in Xamarin Mac can't seem to obtain the Getter methods unless the getter is used somewhere in the code.
      // The getter of all properties is required to serialize the Raygun messages to JSON.
      return string.Format("[RaygunErrorMessage: InnerError={0}, Data={1}, ClassName={2}, Message={3}, StackTrace={4}]", InnerError, Data, ClassName, Message, StackTrace);
    }
  }
}

