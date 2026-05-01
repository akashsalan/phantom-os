using System;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace PhantomOS
{
    /// <summary>
    /// Event args carrying a WAV-formatted audio chunk for real-time transcription.
    /// </summary>
    public class AudioChunkEventArgs : EventArgs
    {
        public byte[] WavData { get; }
        public AudioChunkEventArgs(byte[] wavData) => WavData = wavData;
    }

    public class AudioService
    {
        private WasapiLoopbackCapture? _capture;
        private WaveFileWriter? _writer;
        private string _currentFilePath = "";
        private bool _isRecording;

        // Chunked streaming fields
        private MemoryStream? _chunkBuffer;
        private WaveFileWriter? _chunkWriter;
        private System.Threading.Timer? _chunkTimer;
        private WaveFormat? _waveFormat;
        private readonly object _chunkLock = new();
        private const int ChunkIntervalMs = 3000; // Send a chunk every 3 seconds

        public bool IsRecording => _isRecording;

        /// <summary>
        /// Fires every ~3 seconds with a WAV-formatted audio chunk for real-time transcription.
        /// </summary>
        public event EventHandler<AudioChunkEventArgs>? AudioChunkReady;

        public void StartRecording()
        {
            if (_isRecording) return;

            try
            {
                _currentFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"phantom_audio_{Guid.NewGuid()}.wav");

                _capture = new WasapiLoopbackCapture();
                _waveFormat = _capture.WaveFormat;
                _writer = new WaveFileWriter(_currentFilePath, _waveFormat);

                // Initialize chunk buffer for streaming transcription
                _chunkBuffer = new MemoryStream();
                _chunkWriter = new WaveFileWriter(_chunkBuffer, _waveFormat);

                _capture.DataAvailable += (s, a) =>
                {
                    try
                    {
                        // Write to main file
                        _writer?.Write(a.Buffer, 0, a.BytesRecorded);

                        // Write to chunk buffer
                        lock (_chunkLock)
                        {
                            _chunkWriter?.Write(a.Buffer, 0, a.BytesRecorded);
                        }
                    }
                    catch { /* Writer may be disposed during stop */ }
                };

                _capture.RecordingStopped += (s, a) =>
                {
                    // If not stopped intentionally, clean up
                    if (_isRecording) 
                    {
                        Cleanup();
                    }
                };

                // Start the periodic chunk timer
                _chunkTimer = new System.Threading.Timer(
                    _ => FlushChunk(),
                    null,
                    ChunkIntervalMs,
                    ChunkIntervalMs);

                _capture.StartRecording();
                _isRecording = true;
            }
            catch (Exception)
            {
                _isRecording = false;
                _writer?.Dispose();
                _writer = null;
                _capture?.Dispose();
                _capture = null;
                _chunkTimer?.Dispose();
                _chunkTimer = null;
                _chunkBuffer?.Dispose();
                _chunkBuffer = null;
            }
        }

        private void FlushChunk()
        {
            byte[]? wavData = null;

            lock (_chunkLock)
            {
                try
                {
                    if (_chunkWriter == null || _chunkBuffer == null || _waveFormat == null)
                        return;

                    // Only flush if we have data beyond the WAV header
                    if (_chunkBuffer.Length <= 44)
                        return;

                    // Flush the writer to ensure header is written
                    _chunkWriter.Flush();

                    wavData = _chunkBuffer.ToArray();

                    // Reset chunk buffer for next interval
                    _chunkWriter.Dispose();
                    _chunkBuffer = new MemoryStream();
                    _chunkWriter = new WaveFileWriter(_chunkBuffer, _waveFormat);
                }
                catch { return; }
            }

            if (wavData != null && wavData.Length > 100)
            {
                AudioChunkReady?.Invoke(this, new AudioChunkEventArgs(wavData));
            }
        }

        public async Task<(string FilePath, byte[]? FinalChunk)> StopRecordingAsync()
        {
            if (!_isRecording) return ("", null);

            var tcs = new TaskCompletionSource<bool>();
            byte[]? finalChunkData = null;

            EventHandler<StoppedEventArgs> handler = null!;
            handler = (s, a) =>
            {
                // Unsubscribe to avoid memory leaks
                if (_capture != null) _capture.RecordingStopped -= handler;
                
                // Flush final chunk explicitly
                lock (_chunkLock)
                {
                    try
                    {
                        if (_chunkWriter != null && _chunkBuffer != null && _chunkBuffer.Length > 44)
                        {
                            _chunkWriter.Flush();
                            finalChunkData = _chunkBuffer.ToArray();
                        }
                    }
                    catch { }
                }

                Cleanup();
                tcs.TrySetResult(true);
            };

            if (_capture != null)
            {
                _capture.RecordingStopped += handler;
                try
                {
                    _capture.StopRecording();
                }
                catch
                {
                    tcs.TrySetResult(false);
                }
            }
            else
            {
                tcs.TrySetResult(false);
            }

            // Wait with a timeout to prevent deadlocks
            await Task.WhenAny(tcs.Task, Task.Delay(2000));

            return (_currentFilePath, finalChunkData);
        }

        private void Cleanup()
        {
            _chunkTimer?.Dispose();
            _chunkTimer = null;

            _writer?.Dispose();
            _writer = null;

            _capture?.Dispose();
            _capture = null;

            _isRecording = false;
        }
    }
}
