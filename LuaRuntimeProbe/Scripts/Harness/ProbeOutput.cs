using System;
using System.Collections.Generic;
using System.Globalization;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>
    /// Collects report lines and emits them exactly once, when the command
    /// finishes.  Nothing here writes to the game log while a benchmark runs.
    /// </summary>
    public sealed class ProbeOutput
    {
        public const int MaxLines = 500;

        private readonly List<string> _lines = new List<string>();
        private int _suppressed;

        public IList<string> Lines
        {
            get { return _lines; }
        }

        public int SuppressedCount
        {
            get { return _suppressed; }
        }

        public void Line(string text)
        {
            if (_lines.Count >= MaxLines)
            {
                _suppressed++;
                return;
            }

            _lines.Add(text ?? string.Empty);
        }

        public void Line(string format, params object[] args)
        {
            Line(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        public void Blank()
        {
            Line(string.Empty);
        }

        /// <summary>
        /// Writes the buffered lines to the console command result list and to
        /// the game log.  The game log copy is what makes the run verifiable
        /// afterwards even when the console list is unavailable.
        /// </summary>
        public void Flush()
        {
            if (_suppressed > 0)
            {
                _lines.Add("... " + _suppressed + " further line(s) suppressed (ProbeOutput.MaxLines=" + MaxLines + ")");
            }

            SdtdConsole console = null;
            try
            {
                console = SingletonMonoBehaviour<SdtdConsole>.Instance;
            }
            catch (Exception)
            {
                console = null;
            }

            foreach (string line in _lines)
            {
                if (console != null)
                {
                    try
                    {
                        console.Output(line);
                    }
                    catch (Exception)
                    {
                        // Fall through to the log-only path below.
                    }
                }

                Log.Out(line);
            }
        }
    }
}
