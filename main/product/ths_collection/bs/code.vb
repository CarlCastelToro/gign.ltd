Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Web
Imports HttpStatusCode = System.Net.HttpStatusCode

Public Class Form1
#Region "内部变量声明"
    Private Port As Integer = 14919
    Private isServerRunning As Boolean
    Private log As Log
    Private scshotCount As Integer
    Private pastScshotCount As Integer
    Private listener As HttpListener
    Private serverThread As Thread
    Private serverStopFlag As Boolean = False
    Private ExitFlag As Boolean = False
    Private TempString As String = ""
    Private filesFolderPath As String = Path.Combine(Application.StartupPath, "files")
    Private folderName As String = "files"
#End Region
#Region "Form1 内部代码"
    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        log = New Log(rtbLog)
        log.WriteLine("Log system enabled")
        log.WriteLine("For more info please contact IAmSystem32@outlook.com or visit gign.ltd")
        Dim currentDirectory As String = Application.StartupPath
        Dim configFilePath As String = Path.Combine(currentDirectory, "foldername.txt")
        log.WriteLine("Deciding on folder name")
        If Not File.Exists(configFilePath) Then
            Try
                File.WriteAllText(configFilePath, "files")
                log.WriteLine("No foldername.txt, using 'files' as default")
            Catch ex As Exception
                log.WriteLine("Failed to create foldername.txt")
                LogException(ex)
            End Try
        Else
            folderName = File.ReadAllText(configFilePath)
            log.WriteLine($"Confirm the folder name as '{folderName}'")
        End If
        log.WriteLine($"Checking availability of the '{folderName}' folder")
        If Not Directory.Exists(Path.Combine(Application.StartupPath, folderName)) Then
            Try
                log.WriteLine($"'{folderName}' folder not found, creating it automatically")
                Directory.CreateDirectory(Path.Combine(Application.StartupPath, folderName))
            Catch ex As Exception
                log.WriteLine("Failed to create folder")
                LogException(ex)
            End Try
        End If
        filesFolderPath = Path.Combine(Application.StartupPath, folderName)
        log.WriteLine("-----------------------------------------------")
    End Sub

    Sub Server_Start()
        Try
            listener = New HttpListener()
            log.WriteLine($"Listening for requests on http/tcp port {Port}")
            listener.Prefixes.Add($"http://*:{Port}/")
            listener.Start()
            serverStopFlag = False
            serverThread = New Thread(AddressOf Listen_)
            serverThread.Start()
            isServerRunning = True
            btnStartServer.Enabled = False
            btnStopServer.Enabled = True
            nudPortSelect.Enabled = False
        Catch ex As Exception
            isServerRunning = False
            log.WriteLine("Failed to start server")
            log.WriteLine("------Exception Info------")
            log.WriteLine(ex.Message)
            log.WriteLine(ex.StackTrace)
            log.WriteLine(ex.TargetSite.ToString)
            log.WriteLine(ex.GetType.ToString)
            log.WriteLine("--------------------------")
        End Try
    End Sub

    Sub Server_Stop()
        Try
            If listener IsNot Nothing Then
                listener.Prefixes.Remove($"http://*:{Port}/")
                listener.Stop()
            End If
            serverStopFlag = True
            If serverThread IsNot Nothing AndAlso serverThread.IsAlive Then
                serverThread.Join()
            End If
            log.WriteLine("Server has stopped")
        Catch ex As Exception
            log.WriteLine("Failed to stop server")
            log.WriteLine("------Exception Info------")
            log.WriteLine(ex.Message)
            log.WriteLine(ex.StackTrace)
            log.WriteLine(ex.TargetSite.ToString)
            log.WriteLine(ex.GetType.ToString)
            log.WriteLine("--------------------------")
        End Try
        isServerRunning = False
        btnStopServer.Enabled = False
        btnStartServer.Enabled = True
        nudPortSelect.Enabled = True
    End Sub

    Private Sub btnStartServer_Click(sender As Object, e As EventArgs) Handles btnStartServer.Click
        Try
            Server_Start()
        Catch ex As Exception
            log.WriteLine(ex.Message, True)
        End Try
    End Sub

    Private Sub btnStopServer_Click(sender As Object, e As EventArgs) Handles btnStopServer.Click
        Server_Stop()
    End Sub

    Private Sub Form1_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If ExitFlag Then Exit Sub
        If isServerRunning Then
            e.Cancel = True
            log.WriteLine("Error: cannot stop server while it is running")
            MessageBox.Show("Error: cannot stop server while it is running", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
    End Sub

    Private Sub nudPortSelect_ValueChanged(sender As Object, e As EventArgs) Handles nudPortSelect.ValueChanged
        Port = nudPortSelect.Value
    End Sub

#End Region
#Region "HttpCore"
    Private Sub ProcessClientRequest(request As HttpListenerRequest, response As HttpListenerResponse)
        Dim logRequest As Boolean = True
        Try
            Dim parameters As New UrlParameters()
            Dim processedUrl As String = UrlHelper.ProcessUrl(request.RawUrl, parameters)

            Dim requestHost As String = request.UserHostName
            Dim requestMethod As String = request.HttpMethod
            Dim clientAddress As String = request.RemoteEndPoint.ToString()
            If logRequest Then
                log.WriteLine("------Request Begin------")
                log.WriteLine("Client Address: " & clientAddress)
                log.WriteLine("Request Host: " & requestHost)
                log.WriteLine("Request Method: " & requestMethod)
                log.WriteLine("Original URL: " & request.RawUrl)
                log.WriteLine("Processed URL: " & processedUrl)

                If parameters.Count > 0 Then
                    log.WriteLine("Parameters:")
                    For Each key As String In parameters.GetKeys()
                        log.WriteLine($"- {key}: {parameters.GetValue(key)}")
                    Next
                End If

                For Each header As String In request.Headers
                    log.WriteLine(header & ": " & request.Headers(header))
                Next
            End If

            If logRequest Then log.Write("Server response: ")
            If processedUrl = "/tempstring" Then
                HandleTempStringRequest(request, response, parameters)
                If logRequest Then log.WriteLine("-------Request End-------")
                Return
            End If
            Select Case True
                Case processedUrl = "/favicon.ico"
                    HandleFaviconRequest(response)
                Case processedUrl = "/!503"
                    Handle503Request(response)
                Case processedUrl.StartsWith("/!hide")
                    Visible = False
                    ExitFlag = True
                Case processedUrl.StartsWith("/!show")
                    Visible = True
                    ExitFlag = False
                    Show()
                Case processedUrl.StartsWith("/!exit")
                    SendHttpResponse(response, HttpStatusCode.OK, "text/html; charset=utf-8", "Success")
                    ExitFlag = True
                    Application.Exit()
                    End
                Case processedUrl.StartsWith("/!cleanlog")
                    log.Clean()
                Case Else
                    HandleFileRequest(request, response, processedUrl, parameters).Wait()
            End Select

            If logRequest Then
                log.WriteLine("-------Request End-------")
            End If

        Catch ex As Exception
            Dim errorMsg As String = $"Exception: {ex.Message}"
            log.WriteLine(errorMsg)
            Try
                Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(errorMsg)
                response.ContentType = "text/plain; charset=utf-8"
                response.StatusCode = CInt(HttpStatusCode.InternalServerError)
                response.ContentLength64 = errorBuffer.Length
                response.OutputStream.Write(errorBuffer, 0, errorBuffer.Length)
            Catch
                ' 忽略响应写入异常（可能客户端已断开连接）
            End Try
        Finally
            ' 避免内存泄漏
            response?.OutputStream?.Close()
            response?.Close()
        End Try
    End Sub

    Private Sub HandleClientInNewThread(result As IAsyncResult)
        Dim listener As HttpListener = CType(result.AsyncState, HttpListener)
        Dim clientContext As HttpListenerContext = Nothing

        Try
            clientContext = listener.EndGetContext(result)
            Dim request As HttpListenerRequest = clientContext.Request
            Dim response As HttpListenerResponse = clientContext.Response

            Dim clientThread As New Thread(Sub()
                                               ProcessClientRequest(request, response)
                                           End Sub)

            clientThread.IsBackground = True
            clientThread.Start()
        Catch ex As Exception
            clientContext?.Response?.Close()
            If ex.HResult = -2147467259 Then
                log.WriteLine("Server thread exited.")
            Else
                log.WriteLine($"Exception in obtaining client connection: {ex.Message} HResult: {ex.HResult}")
            End If
        End Try
    End Sub

    Private Sub HandleTempStringRequest(request As HttpListenerRequest, response As HttpListenerResponse, parameters As UrlParameters)
        Try
            If (parameters.ContainsKey("content")) Then
                TempString = parameters.GetValue("content")
                SendHttpResponse(response, HttpStatusCode.OK, "text/plaintext; charset=utf-8", "操作成功完成")
            Else
                SendHttpResponse(response, HttpStatusCode.OK, "text/plaintext; charset=utf-8", TempString)
            End If
        Catch ex As Exception

        End Try
    End Sub

    Private Async Function HandleFileRequest(request As HttpListenerRequest, response As HttpListenerResponse, processedUrl As String, parameters As UrlParameters) As Task
        Dim requestedPath As String = ""
        Try
            ' 根路径处理逻辑
            If processedUrl = "/" Or processedUrl = "" Then
                Dim indexFilePath As String = Path.Combine(filesFolderPath, "index.html")
                ' 检查files文件夹中是否存在index.html
                If Directory.Exists(filesFolderPath) AndAlso File.Exists(indexFilePath) Then
                    ' 如果存在，则使用files文件夹中的index.html
                    requestedPath = indexFilePath
                Else
                    ' 如果不存在，则返回内置主页
                    If Not String.IsNullOrEmpty(HomePage) Then
                        response.ContentType = "text/html; charset=utf-8"
                        response.Headers.Add("Content-Encoding", "utf-8")
                        response.ContentLength64 = Encoding.UTF8.GetBytes(HomePage).Length
                        response.StatusCode = CInt(HttpStatusCode.OK)

                        Using output As Stream = response.OutputStream
                            Await output.WriteAsync(Encoding.UTF8.GetBytes(HomePage), 0, Encoding.UTF8.GetBytes(HomePage).Length)
                            Await output.FlushAsync()
                        End Using
                        log.WriteLine("Served built-in HomePage for root path")
                        Return
                    Else
                        Throw New FileNotFoundException("HomePage not available", "")
                    End If
                End If
            Else
                ' 非根路径处理，映射到files文件夹
                Dim relativePath As String = processedUrl.TrimStart("/"c).Replace("/", "\")
                requestedPath = Path.Combine(filesFolderPath, relativePath)

                ' 首先尝试直接路径
                If Not File.Exists(requestedPath) Then
                    ' 如果文件不存在，尝试基于Referer头信息进行智能路径修正
                    Dim referer As String = request.Headers("Referer")
                    If Not String.IsNullOrEmpty(referer) Then
                        Try
                            Dim refererUri As New Uri(referer)
                            Dim refererPath As String = refererUri.LocalPath

                            ' 获取引用页面所在的目录
                            Dim refererDir As String = Path.GetDirectoryName(refererPath)
                            If Not String.IsNullOrEmpty(refererDir) Then
                                ' 1. 尝试在引用页面的目录中查找资源
                                Dim refererDirPath As String = refererDir.TrimStart("/"c).Replace("/", "\")
                                Dim fileName As String = Path.GetFileName(processedUrl)
                                Dim candidatePath As String = Path.Combine(filesFolderPath, refererDirPath, fileName)

                                If File.Exists(candidatePath) Then
                                    log.WriteLine($"Found file in referer directory: {candidatePath} (original: {requestedPath})")
                                    requestedPath = candidatePath
                                Else
                                    ' 2. 如果是ICO文件的404错误，尝试在多个可能的位置查找
                                    Dim fileExtension As String = Path.GetExtension(processedUrl).ToLower()

                                    If fileExtension = ".ico" Then
                                        ' 尝试在几个常见位置查找icon.ico
                                        Dim potentialLocations As New List(Of String)

                                        ' 添加原始的常见位置
                                        potentialLocations.Add(Path.Combine(filesFolderPath, Path.GetFileName(processedUrl)))
                                        potentialLocations.Add(Path.Combine(filesFolderPath, "favicon.ico"))

                                        ' 如果有Referer信息，尝试在引用页面的目录中查找
                                        If Not String.IsNullOrEmpty(refererPath) Then
                                            Dim subdirIconPath As String = Path.Combine(filesFolderPath, Path.GetDirectoryName(refererPath).TrimStart("/"c).Replace("/", "\"), Path.GetFileName(processedUrl))
                                            potentialLocations.Insert(0, subdirIconPath) ' 优先检查引用页面目录

                                            log.WriteLine($"Adding referer directory path for icon lookup: {subdirIconPath}")
                                        End If

                                        ' 为/product/icon.ico请求添加特殊处理，检查/product/bs/目录
                                        If processedUrl = "/product/icon.ico" Then
                                            Dim bsIconPath As String = Path.Combine(filesFolderPath, "product", "bs", Path.GetFileName(processedUrl))
                                            potentialLocations.Insert(0, bsIconPath)
                                            log.WriteLine($"Adding special bs directory path for icon lookup: {bsIconPath}")
                                        End If

                                        ' 检查所有可能的位置
                                        For Each location As String In potentialLocations
                                            If File.Exists(location) Then
                                                log.WriteLine($"Found icon in location: {location} (original: {requestedPath})")
                                                requestedPath = location
                                                Exit For
                                            End If
                                        Next
                                    End If
                                End If
                            End If
                        Catch ex As Exception
                            ' 忽略URI解析错误，继续尝试其他方法
                            log.WriteLine($"Error parsing referer: {ex.Message}")
                        End Try
                    End If
                End If
            End If

            ' 检查路径是否指向文件夹（包括以/结尾和不以/结尾的情况）
            ' 首先检查文件是否存在
            If Not File.Exists(requestedPath) Then
                ' 如果文件不存在，检查是否是文件夹
                If Directory.Exists(requestedPath) Then
                    ' 尝试查找文件夹中的index.html
                    Dim folderIndexPath As String = Path.Combine(requestedPath, "index.html")
                    If File.Exists(folderIndexPath) Then
                        requestedPath = folderIndexPath
                        log.WriteLine($"Serving index.html for directory: {processedUrl}")
                    Else
                        ' 文件夹存在但没有index.html，返回404
                        response.StatusCode = 404
                        response.ContentType = "text/html; charset=utf-8"
                        response.Headers.Add("Content-Encoding", "utf-8")
                        response.ContentLength64 = Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl & "/index.html").Length

                        Using output As Stream = response.OutputStream
                            output.Write(Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl & "/index.html"), 0, Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl & "/index.html").Length)
                        End Using

                        log.WriteLine($"Directory exists but no index.html: {requestedPath}")
                        Return
                    End If
                Else
                    ' 如果既不是文件也不是文件夹，返回404
                    response.StatusCode = 404
                    response.ContentType = "text/html; charset=utf-8"
                    response.Headers.Add("Content-Encoding", "utf-8")
                    response.ContentLength64 = Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl).Length

                    Using output As Stream = response.OutputStream
                        output.Write(Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl), 0, Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl).Length)
                    End Using

                    log.WriteLine($"File not found: {requestedPath} for path {processedUrl}")
                    Return
                End If
            End If

            ' 处理文件内容
            Dim fileContent As Byte() = File.ReadAllBytes(requestedPath)
            Dim contentType As String = GetMimeType(Path.GetExtension(requestedPath))

            ' 设置响应头
            response.ContentType = contentType
            response.ContentLength64 = fileContent.Length
            response.StatusCode = CInt(HttpStatusCode.OK)

            ' 添加缓存控制头
            response.Headers.Add("Cache-Control", "max-age=3600")

            ' 写入响应内容
            Using output As Stream = response.OutputStream
                Await output.WriteAsync(fileContent, 0, fileContent.Length)
                Await output.FlushAsync()
            End Using

            log.WriteLine($"Served file: {requestedPath} ({fileContent.Length} bytes)")

        Catch ex As Exception
            ' 异常处理代码
            log.WriteLine($"Error handling file request: {ex.Message}")
            Try
                response.StatusCode = 500
                response.ContentType = "text/html; charset=utf-8"
                Dim errorContent As String = $"<html><body><h1>500 Internal Server Error</h1><p>{ex.Message}</p></body></html>"
                Dim errorBytes As Byte() = Encoding.UTF8.GetBytes(errorContent)
                response.ContentLength64 = errorBytes.Length

                Using output As Stream = response.OutputStream
                    output.Write(errorBytes, 0, errorBytes.Length)
                End Using
            Catch
                ' 忽略响应写入失败的错误
            End Try
        End Try
    End Function


    Private Async Function HandleFileRequest_old(request As HttpListenerRequest, response As HttpListenerResponse, processedUrl As String, parameters As UrlParameters) As Task
        Dim requestedPath As String = ""
        Try
            If processedUrl = "/" Or processedUrl = "" Then
                requestedPath = "\index.html"
            Else
                requestedPath = processedUrl.Replace("/", "\")
            End If

            requestedPath = HttpUtility.UrlDecode(requestedPath)

            Dim fullPath As String = If(File.Exists(Application.StartupPath & requestedPath),
                                       Application.StartupPath & requestedPath,
                                       If(requestedPath.Length > 0 AndAlso File.Exists(requestedPath.Remove(0, 1)),
                                          requestedPath.Remove(0, 1),
                                          Nothing))

            If fullPath Is Nothing OrElse Not File.Exists(fullPath) Then
                If Not String.IsNullOrEmpty(HomePage) Then

                    response.ContentType = "text/html"
                    response.ContentLength64 = Encoding.UTF8.GetBytes(HomePage).Length
                    response.StatusCode = CInt(HttpStatusCode.OK)

                    Using output As Stream = response.OutputStream
                        Await output.WriteAsync(Encoding.UTF8.GetBytes(HomePage), 0, Encoding.UTF8.GetBytes(HomePage).Length)
                        Await output.FlushAsync()
                    End Using
                    log.WriteLine("Served HomePage for path: " & processedUrl)
                    Return
                Else
                    Throw New FileNotFoundException("Requested file not found", requestedPath)
                End If
            End If

            Dim buffer As Byte() = File.ReadAllBytes(fullPath)
            response.ContentType = GetMimeType(fullPath)

            Dim rangeHeader As String = request.Headers("Range")
            If Not String.IsNullOrEmpty(rangeHeader) Then
                Await ProcessRangeRequestAsync(request, response, buffer, rangeHeader)
            Else
                response.ContentLength64 = buffer.Length
                response.StatusCode = CInt(HttpStatusCode.OK)

                Using output As Stream = response.OutputStream
                    Await output.WriteAsync(buffer, 0, buffer.Length)
                    Await output.FlushAsync()
                End Using
            End If

        Catch ex As FileNotFoundException
            response.StatusCode = CInt(HttpStatusCode.NotFound)
            Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(HTTP404Page & requestedPath.Remove(0, 1))
            response.ContentLength64 = errorBuffer.Length
            response.ContentType = "text/html"

            Using output As Stream = response.OutputStream
                output.Write(errorBuffer, 0, errorBuffer.Length)
            End Using

            log.WriteLine($"File not found: {ex.FileName} for path {processedUrl}")
        Catch ex As Exception
            Dim buffer As Byte() = Encoding.UTF8.GetBytes(HTTP404Page & "<br>" & processedUrl)
            response.StatusCode = 404
            response.ContentType = "text/html"
            response.ContentLength64 = buffer.Length

            Using output As Stream = response.OutputStream
                output.Write(buffer, 0, buffer.Length)
            End Using

            log.WriteLine("Failed to return file, return 404 Not Found")
            LogException(ex)
        End Try
    End Function

    Private Async Function ProcessRangeRequestAsync(request As HttpListenerRequest,
                                          response As HttpListenerResponse,
                                          buffer As Byte(),
                                          rangeHeader As String) As Task
        Await Task.Run(Sub() ProcessRangeRequest(request, response, buffer, rangeHeader))
    End Function

    Private Sub Handle503Request(response As HttpListenerResponse)
        Dim buffer As Byte() = Encoding.UTF8.GetBytes(HTTP503Page)
        response.StatusCode = 503
        response.ContentType = "text/html"
        response.ContentLength64 = buffer.Length
        response.OutputStream.Write(buffer, 0, buffer.Length)
        response.OutputStream.Close()
        log.WriteLine("Return 503 Service Unavailable")
    End Sub

    Private Sub HandleExceptionResponse(response As HttpListenerResponse, ex As Exception, message As String)
        Dim buffer As Byte() = Encoding.UTF8.GetBytes(HTTP503Page)
        response.StatusCode = 503
        response.ContentType = "text/html"
        response.ContentLength64 = buffer.Length
        response.OutputStream.Write(buffer, 0, buffer.Length)

        log.WriteLine($"{message}, return 503 Service Unavailable instead")
        LogException(ex)
    End Sub

    Private Async Sub ProcessRangeRequest(request As HttpListenerRequest,
                               response As HttpListenerResponse,
                               buffer As Byte(),
                               rangeHeader As String)
        Try
            Dim rangeParts As String() = rangeHeader.Replace("bytes=", "").Split("-")
            Dim start As Long = 0
            Dim endByte As Long = buffer.Length - 1

            If Not String.IsNullOrEmpty(rangeParts(0)) Then
                Long.TryParse(rangeParts(0), start)
            End If

            If rangeParts.Length > 1 AndAlso Not String.IsNullOrEmpty(rangeParts(1)) Then
                Long.TryParse(rangeParts(1), endByte)
            End If

            start = Math.Max(0, Math.Min(start, buffer.Length - 1))
            endByte = Math.Max(start, Math.Min(endByte, buffer.Length - 1))
            Dim length As Long = endByte - start + 1

            response.StatusCode = CInt(HttpStatusCode.PartialContent)
            response.ContentLength64 = length
            response.AddHeader("Content-Range", $"bytes {start}-{endByte}/{buffer.Length}")
            response.AddHeader("Accept-Ranges", "bytes")

            Using output As Stream = response.OutputStream
                Await output.WriteAsync(buffer, CInt(start), CInt(length))
                Await output.FlushAsync()
            End Using

            log.WriteLine($"Sent range {start}-{endByte} for {request.RawUrl}")

        Catch ex As Exception
            ProcessRangeRequest_Finally(request, response, buffer, rangeHeader, ex)
        End Try
    End Sub

    Private Async Sub ProcessRangeRequest_Finally(request As HttpListenerRequest,
                               response As HttpListenerResponse,
                               buffer As Byte(),
                               rangeHeader As String,
                               ex As Exception)
        response.StatusCode = CInt(HttpStatusCode.OK)
        response.ContentLength64 = buffer.Length
        Using output As Stream = response.OutputStream
            Await output.WriteAsync(buffer, 0, buffer.Length)
        End Using
        log.WriteLine($"Range request failed: {ex.Message}, sending full content instead")
    End Sub

    Private Sub HandleFaviconRequest(response As HttpListenerResponse)
        Dim favicon As Icon = My.Resources.favicon
        Dim faviconBytes As Byte() = IconToByteArray(favicon)

        response.ContentType = "image/x-icon"
        response.ContentLength64 = faviconBytes.Length
        response.OutputStream.Write(faviconBytes, 0, faviconBytes.Length)
        response.OutputStream.Close()
        log.WriteLine("Sent favicon.ico to client")
    End Sub
#End Region
#Region "HttpAssistant"
    Public Function FindByteSequence(sourceBytes As Byte(), targetSequence As Byte(), startSearchIndex As Integer) As Integer
        If sourceBytes Is Nothing OrElse targetSequence Is Nothing _
           OrElse sourceBytes.Length < targetSequence.Length _
           OrElse startSearchIndex < 0 _
           OrElse startSearchIndex > sourceBytes.Length - targetSequence.Length Then
            Return -1
        End If

        For i As Integer = startSearchIndex To sourceBytes.Length - targetSequence.Length
            Dim isMatch As Boolean = True
            For j As Integer = 0 To targetSequence.Length - 1
                If sourceBytes(i + j) <> targetSequence(j) Then
                    isMatch = False
                    Exit For
                End If
            Next
            If isMatch Then Return i
        Next

        Return -1
    End Function

    Public Function GetMimeType(filePath As String) As String
        Dim extension As String = Path.GetExtension(filePath).ToLowerInvariant()

        Select Case extension
            Case ".htm", ".html" : Return "text/html"
            Case ".txt" : Return "text/plain"
            Case ".css" : Return "text/css"
            Case ".js" : Return "text/javascript"
            Case ".gif" : Return "image/gif"
            Case ".jpg", ".jpeg" : Return "image/jpeg"
            Case ".png" : Return "image/png"
            Case ".svg" : Return "image/svg+xml"
            Case ".mp3" : Return "audio/mpeg"
            Case ".wav" : Return "audio/wav"
            Case ".flac" : Return "audio/flac"
            Case ".ogg" : Return "audio/ogg"
            Case ".mp4" : Return "video/mp4"
            Case ".pdf" : Return "application/pdf"
            Case ".zip" : Return "application/zip"
            Case ".json" : Return "application/json"
            Case ".xml" : Return "application/xml"
            Case ".ico" : Return "image/x-icon"
            Case Else : Return "application/octet-stream"
        End Select
    End Function

    Public Sub SendHttpResponse(response As HttpListenerResponse, statusCode As HttpStatusCode, contentType As String, content As String)
        Try
            Dim responseBytes As Byte() = Encoding.UTF8.GetBytes(content)
            response.StatusCode = CInt(statusCode)
            response.ContentType = contentType
            response.ContentLength64 = responseBytes.Length
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length)
        Finally
            If response.OutputStream IsNot Nothing Then
                response.OutputStream.Close()
            End If
        End Try
    End Sub

    Public Function IconToByteArray(icon As Icon) As Byte()
        Using ms As New MemoryStream()
            icon.Save(ms)
            Return ms.ToArray()
        End Using
    End Function
#End Region

    Async Sub Listen_()
        Try
            If listener Is Nothing Then
                listener = New HttpListener()
                listener.Prefixes.Add($"http://*:{Port}/")
                listener.Start()
            End If
            While Not serverStopFlag
                Try
                    Dim contextAsync As IAsyncResult = listener.BeginGetContext(
                    AddressOf HandleClientInNewThread,
                    listener
                )

                    contextAsync.AsyncWaitHandle.WaitOne()
                Catch ex As Exception
                    If Not serverStopFlag Then
                        log.WriteLine($"Exception in obtaining client connection: {ex.Message}")
                    End If
                End Try
            End While
        Catch ex As Exception
            log.WriteLine($"Listener thread exception: {ex.Message}")
        Finally
            If listener IsNot Nothing AndAlso listener.IsListening Then
                listener.Stop()
                listener.Close()
                log.WriteLine("Stop Server.")
            End If
            listener = Nothing
        End Try
    End Sub
    Private Sub LogException(ex As Exception)
        log.WriteLine("------Exception Info------")
        log.WriteLine(ex.Message)
        log.WriteLine(ex.StackTrace)
        log.WriteLine(ex.TargetSite.ToString)
        log.WriteLine(ex.GetType.ToString)
        log.WriteLine("--------------------------")
    End Sub

    Private Sub CheckBox3_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox3.CheckedChanged
        log.Lock = CheckBox3.Checked
    End Sub
End Class

Public Class Log
    Implements IDisposable

    Private ReadOnly rtf As RichTextBox
    Public Property Lock As Boolean = False
    Private _fileStream As FileStream
    Private _writer As StreamWriter

    Public Sub New(rtf As RichTextBox)
        Me.rtf = rtf
        rtf.ReadOnly = True
        rtf.ForeColor = Color.White
        rtf.BackColor = Color.Black
        _fileStream = New FileStream(
            Application.StartupPath & $"\{rtf.Name}.log",
            FileMode.Append,
            FileAccess.Write,
            FileShare.None
        )
        _writer = New StreamWriter(_fileStream) With {.AutoFlush = True}
    End Sub

    Public Sub WriteLine(text As String, Optional timehead As Boolean = True)
        If Not Lock Then
            If rtf.InvokeRequired Then
                rtf.Invoke(New Action(Of String)(AddressOf WriteLine), text)
            Else
                Dim currentCaretPosition As Integer = rtf.SelectionStart
                Dim isCaretAtEnd As Boolean = (currentCaretPosition = rtf.TextLength)

                If timehead Then Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]")
                rtf.AppendText(text & vbCrLf)

                UpdateCaretPosition(isCaretAtEnd)
            End If
            If timehead Then _writer.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]")
            _writer.WriteLine(text)
        End If
    End Sub

    Public Sub Write(text As String)
        If Not Lock Then
            If rtf.InvokeRequired Then
                rtf.Invoke(New Action(Of String)(AddressOf Write), text)
            Else
                Dim currentCaretPosition As Integer = rtf.SelectionStart
                Dim isCaretAtEnd As Boolean = (currentCaretPosition = rtf.TextLength)

                rtf.AppendText(text)

                UpdateCaretPosition(isCaretAtEnd)
            End If
            _writer.Write(text)
        End If
    End Sub

    Public Sub Clean()
        If rtf.InvokeRequired Then
            rtf.Invoke(New Action(AddressOf Clean))
        Else
            rtf.Text = Nothing
        End If
    End Sub

    Private Sub UpdateCaretPosition(isCaretAtEnd As Boolean)
        If isCaretAtEnd Then
            rtf.SelectionStart = rtf.TextLength
            rtf.ScrollToCaret()
        Else
            rtf.SelectionStart = rtf.SelectionStart
            rtf.ScrollToCaret()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _writer IsNot Nothing Then
            _writer.Dispose()
        End If

        If _fileStream IsNot Nothing Then
            _fileStream.Dispose()
        End If
    End Sub
End Class

Public Class UrlHelper
    Public Shared Function ProcessUrl(rawUrl As String, ByRef parameters As UrlParameters) As String
        If String.IsNullOrEmpty(rawUrl) Then
            Return rawUrl
        End If

        Dim queryIndex As Integer = rawUrl.IndexOf("?")
        If queryIndex = -1 Then
            Return rawUrl
        End If

        Dim baseUrl As String = rawUrl.Substring(0, queryIndex)

        Dim queryString As String = rawUrl.Substring(queryIndex + 1)
        Dim paramPairs() As String = queryString.Split("&"c)

        For Each pair As String In paramPairs
            If Not String.IsNullOrEmpty(pair) Then
                Dim keyValue() As String = pair.Split(New Char() {"="c}, 2)
                Dim key As String = If(keyValue.Length > 0, HttpUtility.UrlDecode(keyValue(0)), "")
                Dim value As String = If(keyValue.Length > 1, HttpUtility.UrlDecode(keyValue(1)), "")

                If Not String.IsNullOrEmpty(key) Then
                    parameters.Add(key, value)
                End If
            End If
        Next

        Return baseUrl
    End Function
End Class

Public Class UrlParameters
    Private params As New Dictionary(Of String, String)()

    Public Sub Add(key As String, value As String)
        If Not params.ContainsKey(key) Then
            params.Add(key, value)
        Else
            params(key) = value
        End If
    End Sub

    Public Function GetValue(key As String) As String
        If params.ContainsKey(key) Then
            Return params(key)
        End If
        Return Nothing
    End Function

    Public Function ContainsKey(key As String) As Boolean
        Return params.ContainsKey(key)
    End Function

    Public Function GetKeys() As IEnumerable(Of String)
        Return params.Keys
    End Function

    Public ReadOnly Property Count As Integer
        Get
            Return params.Count
        End Get
    End Property
End Class

Module InlinePages
    Public Const HTTP503Page As String = "<html>

<head>
    <meta charset=""UTF-8"">
    <title>503 Service Unavailable</title>
</head>

<body>
    <h1>503 Service Unavailable</h1>
    <p>The server is not ready to handle the request.</p>
</body>

</html>"
    Public Const HTTP404Page As String = "<html>

<head>
    <meta charset=""UTF-8"">
    <title>404 Not Found</title>
</head>

<body>
    <h1>404 Not Found</h1>
    <p>The requested resource was not found on this server.</p>
</body>

</html>"
    Public Const HTTP405Page As String = "<html>

<head>
    <meta charset=""UTF-8"">
    <title>405 Method Not Allowed</title>
</head>

<body>
    <h1>405 Method Not Allowed</h1>
    <p>Only GET (loading form) and POST (uploading files) requests are supported.</p>
</body>

</html>"
    Public Const HomePage As String = "<html>

<head>
    <meta charset=""UTF-8"">
    <title>TestHttpServer</title>
</head>

<body>
    <h1>BasicHttpServer</h1>
    <p>Private server application for test and development.</p>
    <p>SSL is not support at this time.</p>
    <p>This is a built-in homepage. When the server is ready with the index.html file, it will return its content instead of this page.</p>
    <p><a href = ""mailto:IAmSystem32@outlook.com"">&copy;I Am System32 2025</a></p>
</body>

</html>"
End Module