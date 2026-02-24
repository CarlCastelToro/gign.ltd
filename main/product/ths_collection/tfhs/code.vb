Imports System.ComponentModel
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Web
Imports HttpStatusCode = System.Net.HttpStatusCode
Public Class Form1
#Region "内部变量声明"
    Private Port As Integer = 14922
    Private isServerRunning As Boolean
    Private log As Log
    Private listener As HttpListener
    Private serverThread As Thread
    Private serverStopFlag As Boolean = False
    Private ExitFlag As Boolean = False
#End Region
#Region "Form1 内部代码"
    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        log = New Log(RichTextBox1)
        log.WriteLine("Log system enabled")
        log.WriteLine("https method now unavailable, for more info please contact IAmSystem32@outlook.com")
        log.WriteLine("-----------------------------------------------")
    End Sub

    Sub Server_Start()
        Try
            Dim a As String = If(CheckBox1.Checked, "s", "")
            listener = New HttpListener()
            log.WriteLine($"Listening for requests on http{a} port {Port}")
            listener.Prefixes.Add($"http{a}://*:{Port}/")
            listener.Start()
            serverStopFlag = False
            serverThread = New Thread(AddressOf Listen_)
            serverThread.Start()
            isServerRunning = True
            Button1.Enabled = False
            Button2.Enabled = True
            NumericUpDown1.Enabled = False
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
            Dim a As String = If(CheckBox1.Checked, "s", "")
            If listener IsNot Nothing Then
                listener.Prefixes.Remove($"http{a}://*:{Port}/")
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
        Button2.Enabled = False
        Button1.Enabled = True
        NumericUpDown1.Enabled = True
    End Sub
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Try
            Server_Start()
        Catch ex As Exception
            log.WriteLine(ex.Message, True)
        End Try
    End Sub
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Server_Stop()
    End Sub
    Private Sub Form1_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If ExitFlag Then Exit Sub
        If isServerRunning Then
            e.Cancel = True
            log.WriteLine("Error: cannot stop server while it is running")
        End If
    End Sub
    Private Sub DebugButton_Click(sender As Object, e As EventArgs) Handles DebugButton.Click
        Shell($"netsh advfirewall firewall add rule name=""Allow HTTP ({Port}) Inbound"" dir=in action=allow protocol=TCP localport={Port} enable=yes profile=any remoteip=any")
        Shell($"netsh advfirewall firewall add rule name=""Allow HTTP ({Port}) Outbound"" dir=out action=allow protocol=TCP localport={Port}")
        Server_Start()
    End Sub
#End Region
#Region "HttpCore"
    Private Sub ProcessClientRequest(request As HttpListenerRequest, response As HttpListenerResponse)
        Dim logRequest As Boolean = True
        Try
            Dim clientIp As String = request.RemoteEndPoint.Address.ToString()
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
            Select Case True
                Case processedUrl = "/favicon.ico"
                    HandleFaviconRequest(response)
                Case processedUrl.StartsWith("/upload")
                    HandleUploadRequest(request, response, logRequest).Wait()
                Case processedUrl.StartsWith("/files")
                    HandleFileListRequest(request, response)
                Case processedUrl = "/503"
                    Handle503Request(response)
                Case processedUrl.StartsWith("/hide")
                    Visible = False
                    ExitFlag = True
                Case processedUrl.StartsWith("/show")
                    Visible = True
                    ExitFlag = False
                    Show()
                Case processedUrl.StartsWith("/exit")
                    SendHttpResponse(response, HttpStatusCode.OK, "text/html; charset=utf-8", "Success")
                    ExitFlag = True
                    Application.Exit()
                    End
                Case processedUrl.StartsWith("/cleanlog")
                    log.Clean()
                Case processedUrl.StartsWith("/download")
                    HandleFileDownload(request, response, parameters)
                Case processedUrl.StartsWith("/delete")
                    HandleFileDelete(request, response, parameters)
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
    Private Sub HandleMethodNotAllowed(response As HttpListenerResponse)
        Dim notAllowedBuffer As Byte() = Encoding.UTF8.GetBytes(HTTP405Page)
        response.StatusCode = 405
        response.ContentType = "text/html; charset=utf-8"
        response.ContentLength64 = notAllowedBuffer.Length
        response.AddHeader("Allow", "GET, POST")
        response.OutputStream.Write(notAllowedBuffer, 0, notAllowedBuffer.Length)
        response.OutputStream.Close()
        log.WriteLine($"Client used unsupported method, returned 405")
    End Sub
    Private Async Function HandleFileRequest(request As HttpListenerRequest, response As HttpListenerResponse, processedUrl As String, parameters As UrlParameters) As Task
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
    Private Async Function HandleFileListRequest(request As HttpListenerRequest, response As HttpListenerResponse) As Task
        Try
            ' 定义上传文件存储目录（与上传逻辑保持一致）
            Dim uploadDir As String = Path.Combine(Application.StartupPath, "Files")
            If Not Directory.Exists(uploadDir) Then
                ' 目录不存在时返回空数组
                Dim emptyJson As String = "[]"
                Dim emptyBuffer As Byte() = Encoding.UTF8.GetBytes(emptyJson)
                response.StatusCode = CInt(HttpStatusCode.OK)
                response.ContentType = "application/json; charset=utf-8"
                response.ContentLength64 = emptyBuffer.Length
                Using output As Stream = response.OutputStream
                    Await output.WriteAsync(emptyBuffer, 0, emptyBuffer.Length)
                    Await output.FlushAsync()
                End Using
                log.WriteLine("文件列表请求：上传目录不存在，返回空列表")
                Return
            End If

            ' 获取目录下所有文件（排除文件夹和临时文件）
            Dim fileInfos As New List(Of FileInfo)()
            For Each filePath As String In Directory.GetFiles(uploadDir)
                Try
                    Dim fi As New FileInfo(filePath)
                    ' 排除隐藏文件/系统文件，可根据需要调整过滤规则
                    If Not fi.Attributes.HasFlag(FileAttributes.Hidden) AndAlso Not fi.Attributes.HasFlag(FileAttributes.System) Then
                        fileInfos.Add(fi)
                    End If
                Catch ex As Exception
                    log.WriteLine($"跳过无效文件 {filePath}：{ex.Message}")
                End Try
            Next

            ' 按文件创建时间倒序排序（最新创建的在前），也可改为 LastWriteTime（修改时间）
            fileInfos = fileInfos.OrderByDescending(Function(f) f.CreationTime).ToList()

            ' 构建JSON数据（适配前端字段：name、size、ctime（创建时间））
            Dim jsonBuilder As New StringBuilder()
            jsonBuilder.Append("[")
            For i As Integer = 0 To fileInfos.Count - 1
                Dim fi As FileInfo = fileInfos(i)
                Dim originalFileName As String = fi.Name
                ' 格式化创建时间为 yyyy/MM/dd HH:mm:ss
                Dim createTimeStr As String = fi.CreationTime.ToString("yyyy/MM/dd HH:mm:ss")

                ' 添加JSON对象（转义特殊字符防止JSON格式错误）
                jsonBuilder.AppendFormat("{{""name"":""{0}"",""size"":{1},""ctime"":""{2}""}}",
            EscapeJsonString(originalFileName),
            fi.Length,
            EscapeJsonString(createTimeStr)) ' 新增创建时间字段

                ' 最后一个元素不加逗号
                If i < fileInfos.Count - 1 Then
                    jsonBuilder.Append(",")
                End If
            Next
            jsonBuilder.Append("]")

            ' 转换为字节数组并返回响应
            Dim jsonBuffer As Byte() = Encoding.UTF8.GetBytes(jsonBuilder.ToString())
            response.StatusCode = CInt(HttpStatusCode.OK)
            response.ContentType = "application/json; charset=utf-8"
            ' 解决跨域问题（可选，根据实际需求添加）
            response.AddHeader("Access-Control-Allow-Origin", "*")
            response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS")
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type")
            response.ContentLength64 = jsonBuffer.Length

            Using output As Stream = response.OutputStream
                Await output.WriteAsync(jsonBuffer, 0, jsonBuffer.Length)
                Await output.FlushAsync()
            End Using

            log.WriteLine($"文件列表请求：成功返回 {fileInfos.Count} 个文件")

        Catch ex As Exception
            ' 异常处理：返回500错误和错误信息
            Dim errorJson As String = $"{{""error"":""{EscapeJsonString(ex.Message)}""}}"
            Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(errorJson)
            response.StatusCode = CInt(HttpStatusCode.InternalServerError)
            response.ContentType = "application/json; charset=utf-8"
            response.ContentLength64 = errorBuffer.Length

            Using output As Stream = response.OutputStream
                output.Write(errorBuffer, 0, errorBuffer.Length)
            End Using

            log.WriteLine($"文件列表请求失败：{ex.Message}")
            LogException(ex)
        End Try
    End Function

    ' 辅助方法：JSON字符串转义（防止特殊字符导致JSON解析失败）
    Private Function EscapeJsonString(input As String) As String
        If String.IsNullOrEmpty(input) Then Return ""
        Return input.Replace("""", "\""") _
                .Replace("\", "\\") _
                .Replace("/", "\/") _
                .Replace(ControlChars.NewLine, "\n") _
                .Replace(ControlChars.Cr, "\r") _
                .Replace(ControlChars.Tab, "\t")
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
    Private Sub HandleFileDownload(request As HttpListenerRequest, response As HttpListenerResponse, parameters As UrlParameters)
        Try
            If Not parameters.ContainsKey("file") Then
                Throw New Exception("Missing parameters：file")
            End If
            Dim targetFilePath As String = Path.Combine(Application.StartupPath, "Files", parameters.GetValue("file"))
            If Not File.Exists(targetFilePath) Then
                Throw New Exception($"File not found: {targetFilePath}")
            End If
            If Directory.Exists(targetFilePath) Then
                Throw New Exception($"You can not download a folder: {targetFilePath}")
            End If
            Dim fileName As String = Path.GetFileName(targetFilePath)
            response.ContentType = "application/octet-stream"
            response.AddHeader("Content-Disposition", $"attachment; filename=""{HttpUtility.UrlEncode(fileName, Encoding.UTF8)}""")
            response.ContentLength64 = New FileInfo(targetFilePath).Length
            response.StatusCode = CInt(HttpStatusCode.OK)
            Using fileStream As New FileStream(targetFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Using outputStream As Stream = response.OutputStream
                    fileStream.CopyTo(outputStream)
                    outputStream.Flush()
                End Using
            End Using
            log.WriteLine($"File Download Success: {targetFilePath}")
        Catch ex As Exception
            Dim errorMsg As String = $"Download Failed：{ex.Message}"
            Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(errorMsg)
            response.ContentType = "text/plain; charset=utf-8"
            response.ContentLength64 = errorBuffer.Length
            response.StatusCode = CInt(HttpStatusCode.BadRequest)
            Using outputStream As Stream = response.OutputStream
                outputStream.Write(errorBuffer, 0, errorBuffer.Length)
            End Using
            log.WriteLine(errorMsg)
        Finally
            response.OutputStream.Close()
        End Try
    End Sub
    Private Sub HandleFileDelete(request As HttpListenerRequest, response As HttpListenerResponse, parameters As UrlParameters)
        Try
            If Not parameters.ContainsKey("file") Then
                Throw New Exception("Missing parameters：file")
            End If
            Dim targetFilePath As String = Path.Combine(Application.StartupPath, "Files", parameters.GetValue("file"))
            If Not File.Exists(targetFilePath) Then
                Throw New Exception($"File not found: {targetFilePath}")
            End If
            If Directory.Exists(targetFilePath) Then
                Throw New Exception($"You can not operate a folder: {targetFilePath}")
            End If

            ' 执行删除
            IO.File.Delete(targetFilePath)
            Dim Msg As String = $"Deleted File: {targetFilePath}"
            Dim Buffer As Byte() = Encoding.UTF8.GetBytes(Msg)

            ' 修复1：成功时返回 200 OK 状态码
            response.StatusCode = CInt(HttpStatusCode.OK)
            ' 修复2：明确响应类型为文本，禁止浏览器下载
            response.ContentType = "text/plain; charset=utf-8"
            response.AddHeader("Content-Disposition", "inline; filename=null")
            response.ContentLength64 = Buffer.Length

            Using outputStream As Stream = response.OutputStream
                outputStream.Write(Buffer, 0, Buffer.Length)
            End Using
            log.WriteLine($"Deleted File: {targetFilePath}")

        Catch ex As Exception
            Dim errorMsg As String = $"Delete Failed：{ex.Message}"
            Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(errorMsg)

            ' 修复3：根据异常类型返回对应错误码
            If ex.Message.Contains("File not found") Then
                response.StatusCode = CInt(HttpStatusCode.NotFound) ' 文件不存在返回404
            ElseIf ex.Message.Contains("Missing parameters") Then
                response.StatusCode = CInt(HttpStatusCode.BadRequest) ' 参数缺失返回400
            Else
                response.StatusCode = CInt(HttpStatusCode.InternalServerError) ' 其他错误返回500
            End If

            response.ContentType = "text/plain; charset=utf-8"
            response.AddHeader("Content-Disposition", "inline; filename=null")
            response.ContentLength64 = errorBuffer.Length

            Using outputStream As Stream = response.OutputStream
                outputStream.Write(errorBuffer, 0, errorBuffer.Length)
            End Using
            log.WriteLine(errorMsg)

        Finally
            response.OutputStream.Close()
        End Try
    End Sub
    Private Sub HandleFaviconRequest(response As HttpListenerResponse)
        Dim favicon As Icon = My.Resources.Explorer
        Dim faviconBytes As Byte() = IconToByteArray(favicon)

        response.ContentType = "image/x-icon"
        response.ContentLength64 = faviconBytes.Length
        response.OutputStream.Write(faviconBytes, 0, faviconBytes.Length)
        response.OutputStream.Close()
        log.WriteLine("Sent favicon.ico to client")
    End Sub
    Private Async Function HandleUploadRequest(request As HttpListenerRequest, response As HttpListenerResponse, logRequest As Boolean) As Task
        If request.HttpMethod = "GET" Then
            Await HandleUploadFormRequest(response)
        ElseIf request.HttpMethod = "POST" Then
            Await HandleFileUpload(request, response, logRequest)
        Else
            HandleMethodNotAllowed(response)
        End If
    End Function
    Private Async Function HandleUploadFormRequest(response As HttpListenerResponse) As Task
        Try
            Dim formBuffer As Byte() = Encoding.UTF8.GetBytes(UploadFormHtml)
            response.ContentType = "text/html; charset=utf-8"
            response.ContentLength64 = formBuffer.Length
            response.StatusCode = CInt(HttpStatusCode.OK)

            Using outputStream As Stream = response.OutputStream
                Await outputStream.WriteAsync(formBuffer, 0, formBuffer.Length)
                Await outputStream.FlushAsync()
            End Using
            log.WriteLine("The file upload form has been returned to the client.（GET /upload）")
        Catch ex As Exception
            Dim errorHtml As String = $"<html><head><meta charset='UTF-8'><title>Form load failed</title></head><body><h1>Form load failed</h1><p>Exception:  {HttpUtility.HtmlEncode(ex.Message)}</p></body></html>"
            Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(errorHtml)
            response.StatusCode = CInt(HttpStatusCode.InternalServerError)
            response.ContentType = "text/html; charset=utf-8"
            response.ContentLength64 = errorBuffer.Length
            Using outputStream As Stream = response.OutputStream
                outputStream.Write(errorBuffer, 0, errorBuffer.Length)
            End Using
            log.WriteLine("Return form failed." & ex.Message)
            LogException(ex)
        End Try
    End Function
    Private Async Function HandleFileUpload(request As HttpListenerRequest, response As HttpListenerResponse, logRequest As Boolean) As Task
        Try
            Dim requestData As Byte()
            Using memoryStream As New MemoryStream()
                Await request.InputStream.CopyToAsync(memoryStream)
                requestData = memoryStream.ToArray()
            End Using
            If logRequest Then
                Dim bodyPreview As String = If(requestData.Length > 0,
                    Encoding.UTF8.GetString(requestData, 0, Math.Min(1000, requestData.Length)),
                    "")
                log.WriteLine(If(String.IsNullOrEmpty(bodyPreview), "No request body.", "Request Body: " & bodyPreview))
            End If
            If String.IsNullOrEmpty(request.ContentType) OrElse Not request.ContentType.Contains("multipart/form-data") Then
                Throw New Exception("Invalid request format, only multipart/form-data (file upload) is supported.")
            End If
            Dim uploadDir As String = Path.Combine(Application.StartupPath, "Files")
            If Not Directory.Exists(uploadDir) Then
                Directory.CreateDirectory(uploadDir)
                log.WriteLine("Auto create upload folder" & uploadDir)
            End If
            Dim boundary As String = ExtractBoundary(request.ContentType)
            If String.IsNullOrEmpty(boundary) Then
                Throw New Exception("Unable to parse request boundary. Upload failed.")
            End If
            Dim targetFileName As String = ExtractFileName(requestData, boundary)
            If String.IsNullOrEmpty(targetFileName) Then
                Throw New Exception("No file selected for upload.")
            End If
            Dim contentBounds() As Integer = FindFileContentBounds(requestData, boundary)
            Dim contentStartIdx As Integer = contentBounds(0)
            Dim contentEndIdx As Integer = contentBounds(1)
            Dim fileContentLength As Integer = contentEndIdx - contentStartIdx
            If fileContentLength <= 0 Then
                Throw New Exception("The uploaded file content is empty and cannot be saved.")
            End If
            Dim fileContent As Byte() = New Byte(fileContentLength - 1) {}
            Array.Copy(requestData, contentStartIdx, fileContent, 0, fileContentLength)
            Dim uniqueFileName As String = $"{DateTime.Now:yyyyMMddHHmmssfff}_{targetFileName}"
            Dim savePath As String = Path.Combine(uploadDir, uniqueFileName)
            File.WriteAllBytes(savePath, fileContent)
            log.WriteLine($"File uploaded successfully: {uniqueFileName} (Size: {fileContentLength / 1024:F2} KB)")
            log.WriteLine($"Saved path: {savePath}")
            Dim successHtml As String = CreateUploadSuccessHtml(uniqueFileName, fileContentLength)
            Dim successBuffer As Byte() = Encoding.UTF8.GetBytes(successHtml)
            response.ContentType = "text/html; charset=utf-8"
            response.ContentLength64 = successBuffer.Length
            response.StatusCode = CInt(HttpStatusCode.OK)
            Using outputStream As Stream = response.OutputStream
                Await outputStream.WriteAsync(successBuffer, 0, successBuffer.Length)
                Await outputStream.FlushAsync()
            End Using
        Catch ex As Exception
            Dim errorHtml As String = CreateUploadErrorHtml(ex.Message)
            Dim errorBuffer As Byte() = Encoding.UTF8.GetBytes(errorHtml)
            If response.OutputStream.CanWrite Then
                response.StatusCode = CInt(HttpStatusCode.InternalServerError)
                response.ContentType = "text/html; charset=utf-8"
                response.ContentLength64 = errorBuffer.Length
                Using outputStream As Stream = response.OutputStream
                    outputStream.Write(errorBuffer, 0, errorBuffer.Length)
                End Using
            Else
                log.WriteLine("Response stream is closed, unable to return error page.")
            End If
            log.WriteLine("Upload failed: " & ex.Message)
            LogException(ex)
        End Try
    End Function
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
    Public Function ExtractBoundary(contentType As String) As String
        Dim boundaryPrefix As String = "boundary="
        Dim contentTypeParts() As String = contentType.Split(";")
        For Each part As String In contentTypeParts
            If part.Trim().StartsWith(boundaryPrefix, StringComparison.OrdinalIgnoreCase) Then
                Return part.Trim().Substring(boundaryPrefix.Length).Trim("""")
            End If
        Next
        Return String.Empty
    End Function
    Public Function ExtractFileName(requestData As Byte(), boundary As String) As String
        Dim fileFieldBytes As Byte() = Encoding.UTF8.GetBytes("name=""fileToUpload""")
        Dim filenamePrefixBytes As Byte() = Encoding.UTF8.GetBytes("filename=""")
        Dim quoteByte As Byte = Encoding.UTF8.GetBytes("""")(0)
        Dim fileFieldPos As Integer = FindByteSequence(requestData, fileFieldBytes, 0)
        If fileFieldPos = -1 Then Return String.Empty
        Dim filenamePrefixPos As Integer = FindByteSequence(requestData, filenamePrefixBytes, fileFieldPos + fileFieldBytes.Length)
        If filenamePrefixPos = -1 Then Return String.Empty
        Dim filenameStartPos As Integer = filenamePrefixPos + filenamePrefixBytes.Length
        Dim filenameEndPos As Integer = -1
        For i As Integer = filenameStartPos To requestData.Length - 1
            If requestData(i) = quoteByte Then
                filenameEndPos = i
                Exit For
            End If
        Next
        If filenameEndPos <= filenameStartPos Then Return String.Empty
        Dim fileNameBytesLength As Integer = filenameEndPos - filenameStartPos
        Dim fileNameBytes(fileNameBytesLength - 1) As Byte
        Array.Copy(requestData, filenameStartPos, fileNameBytes, 0, fileNameBytesLength)

        Return Path.GetFileName(Encoding.UTF8.GetString(fileNameBytes).Trim())
    End Function
    Public Function FindFileContentBounds(requestData As Byte(), boundary As String) As Integer()
        Dim boundaryBytes As Byte() = Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary)
        Dim emptyLineMarker As Byte() = Encoding.ASCII.GetBytes(vbCrLf & vbCrLf)

        Dim contentStartIdx As Integer = FindByteSequence(requestData, emptyLineMarker, 0)
        If contentStartIdx = -1 Then
            Throw New Exception("Unable to locate file content start position")
        End If
        contentStartIdx += emptyLineMarker.Length
        Dim contentEndIdx As Integer = FindByteSequence(requestData, boundaryBytes, contentStartIdx)
        If contentEndIdx = -1 Then
            Dim endBoundaryBytes As Byte() = Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary & "--")
            contentEndIdx = FindByteSequence(requestData, endBoundaryBytes, contentStartIdx)
            If contentEndIdx = -1 Then
                Throw New Exception("Unable to locate file content end position")
            End If
        End If
        Return New Integer() {contentStartIdx, contentEndIdx}
    End Function
    Public Function CreateUploadSuccessHtml(filename As String, fileSize As Integer) As String
        Return $"<html lang='zh-CN'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'><title>Upload Success</title><style>body{{font-family:Arial,sans-serif;text-align:center;margin-top:5rem;}}h1{{color:#28a745;}}p{{font-size:1.1rem;}}a{{color:#007bff;text-decoration:none;}}</style></head><body><h1>✅ Upload Success</h1><p>File: {HttpUtility.HtmlEncode(filename)}</p><p>File Size: {fileSize / 1024:F2} KB</p><p><a href='/upload'>Return</a></p></body></html>"
    End Function
    Public Function CreateUploadErrorHtml(errorMessage As String) As String
        Return $"<html lang='zh-CN'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'><title>Upload Failed</title><style>body{{font-family:Arial,sans-serif;text-align:center;margin-top:5rem;}}h1{{color:#dc3545;}}p{{font-size:1.1rem;}}a{{color:#007bff;text-decoration:none;}}</style></head><body><h1>❌ Upload Failed</h1><p>Error: {HttpUtility.HtmlEncode(errorMessage)}</p><p><a href='/upload'>Return</a></p></body></html>"
    End Function
    Public Function IconToByteArray(icon As Icon) As Byte()
        Using ms As New MemoryStream()
            icon.Save(ms)
            Return ms.ToArray()
        End Using
    End Function
#End Region
#Disable Warning BC42356 ' 此异步方法缺少 "Await" 运算符，因此将以同步方式运行
    Async Sub Listen_()
#Enable Warning BC42356 ' Not a bug
        Try
            If listener Is Nothing Then
                listener = New HttpListener()
                listener.Prefixes.Add($"http://*:{Port}/")
                listener.Start()
            End If
            While Not serverStopFlag
                Try
                    Dim contextAsync As IAsyncResult = listener.BeginGetContext(AddressOf HandleClientInNewThread, listener)
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

    Private Sub NumericUpDown1_ValueChanged(sender As Object, e As EventArgs) Handles NumericUpDown1.ValueChanged
        Port = NumericUpDown1.Value
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

Imports System.Runtime.InteropServices
Imports System.Windows

Module InlinePages
    Public Const HTTP503Page As String = "<html>

    <head>
    <meta charset ='UTF-8'>
    <title>503 Service Unavailable</title>
    </head>

<body>
    <h1>503 Service Unavailable</h1>
    <p> The server Is Not ready To handle the request.</p>
</body>

</html>"
    Public Const HTTP404Page As String = "<html>

    <head>
    <meta charset ='UTF-8'>
    <title>404 Not Found</title>
    </head>

<body>
    <h1>404 Not Found</h1>
    <p> The requested resource was Not found On this server.</p>
</body>

</html>"
    Public Const HTTP405Page As String = "<html>

    <head>
    <meta charset ='UTF-8'>
    <title>405 Method Not Allowed</title>
    </head>

<body>
    <h1>405 Method Not Allowed</h1>
    <p> Only Get (loading form) And POST (uploading files) requests are supported.</p>
</body>

</html>"
    Public Const DefaultPage As String = "<html>

    <head>
    <meta charset ='UTF-8'>
    <title>TestHttpServer</title>
    </head>

<body>
    <h1> TestHttpServer</h1>
    <p> Private server application For test And development.</p>
    <p> SSL Is Not support at this time.</p>
    <p> <a href = 'mailto:IAmSystem32@outlook.com'>&copy;I Am System32 2025</a></p>
</body>
    
</html>"
    Public Const UploadFormHtml As String = Nothing
    Public HomePage As String = "<!DOCTYPE html>
<html lang='zh-CN'>

<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>文件托管服务器</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f5f5f5;
        }

        .container {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        }

        h1 {
            color: #333;
            text-align: center;
        }

        .search-bar {
            display: flex;
            gap: 10px;
            margin: 20px 0;
        }

        #sortSelect {
            padding: 8px;
            border: 1px solid #ddd;
            border-radius: 4px;
        }

        #searchInput {
            flex: 1;
            padding: 8px;
            border: 1px solid #ddd;
            border-radius: 4px;
        }

        button {
            padding: 8px 16px;
            background: #4CAF50;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
        }

        button:hover {
            background: #45a049;
        }

        .file-list {
            list-style: none;
            padding: 0;
            margin: 20px 0;
        }

        .file-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px;
            border-bottom: 1px solid #eee;
        }

        .file-item:last-child {
            border-bottom: none;
        }

        .file-name {
            flex: 1;
        }

        .file-meta {
            color: #666;
            margin: 0 10px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .file-time {
            color: #999;
            font-size: 12px;
        }

        .file-size {
            font-size: 12px;
        }

        .download-btn {
            color: #2196F3;
            text-decoration: none;
            padding: 4px 8px;
            border: 1px solid #2196F3;
            border-radius: 4px;
        }

        .download-btn:hover {
            background: #2196F3;
            color: white;
        }

        .empty-state {
            color: #666;
            text-align: center;
            padding: 20px;
        }

        .message {
            padding: 10px;
            margin: 10px 0;
            border-radius: 4px;
            display: none;
        }

        .success {
            background: #dff0d8;
            color: #3c763d;
        }

        .error {
            background: #f2dede;
            color: #a94442;
        }

        /* 右上角上传进度悬浮框样式 */
        .upload-progress-panel {
            position: fixed;
            top: 20px;
            right: 20px;
            width: 300px;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.2);
            padding: 15px;
            display: none;
            z-index: 9999;
        }

        .upload-progress-panel .progress-bar {
            height: 8px;
            background-color: #e0e0e0;
            border-radius: 4px;
            overflow: hidden;
            margin: 10px 0;
        }

        .upload-progress-panel .progress-fill {
            height: 100%;
            background-color: #4CAF50;
            width: 0%;
            transition: width 0.3s ease;
        }

        .upload-progress-panel .progress-stats {
            font-size: 12px;
            color: #666;
        }

        .upload-progress-panel .close-btn {
            position: absolute;
            top: 8px;
            right: 8px;
            background: transparent;
            border: none;
            font-size: 16px;
            cursor: pointer;
            color: #999;
            padding: 0;
            width: 20px;
            height: 20px;
            line-height: 20px;
        }
    </style>
</head>

<body>
    <div class='container'>
        <h1>文件托管服务器</h1>

        <div class='search-bar'>
            <!-- 新增排序下拉列表 -->
            <select id='sortSelect'>
                <option value='time-desc'>时间（最新）</option>
                <option value='time-asc'>时间（最旧）</option>
                <option value='size-desc'>文件大小（最大）</option>
                <option value='size-asc'>文件大小（最小）</option>
            </select>
            <input type='text' id='searchInput' placeholder='搜索文件名...'>
            <button onclick='refreshFileList()'>刷新列表</button>
            <button id='uploadBtn'>上传文件</button>
        </div>

        <div class='message success' id='successMessage'></div>
        <div class='message error' id='errorMessage'></div>

        <ul class='file-list' id='fileList'></ul>
    </div>

    <!-- 隐藏的文件选择框 -->
    <input type='file' id='fileInput' name='fileToUpload' style='display: none;' accept='*'>

    <!-- 右上角上传进度面板 -->
    <div class='upload-progress-panel' id='progressPanel'>
        <button class='close-btn' id='progressCloseBtn'>&times;</button>
        <div class='progress-title'>文件上传中</div>
        <div class='progress-bar'>
            <div class='progress-fill' id='progressFill'></div>
        </div>
        <div class='progress-stats' id='progressStats'>0% - 0 B/s - 剩余时间: 计算中...</div>
    </div>

    <script>
        // 全局变量
        let startTime = 0;
        let totalFileSize = 0;
        let allFiles = []; // 存储所有文件，用于排序和过滤

        // 页面初始化入口
        window.onload = () => {
            initEventListeners(); // 初始化所有事件监听
            refreshFileList();    // 初始化文件列表
        };

        // 统一初始化所有事件监听（解耦）
        function initEventListeners() {
            initUploadHandler();    // 上传逻辑
            initSearchHandler();    // 搜索逻辑
            initSortHandler();      // 排序逻辑
            initDeleteHandler();    // 删除逻辑（新增核心）
            initProgressPanelHandler(); // 上传进度面板逻辑
        }

        // 1. 上传功能处理
        function initUploadHandler() {
            const uploadBtn = document.getElementById('uploadBtn');
            const fileInput = document.getElementById('fileInput');

            // 点击上传按钮触发文件选择
            uploadBtn.addEventListener('click', () => {
                fileInput.click();
            });

            // 文件选择后处理上传
            fileInput.addEventListener('change', async (e) => {
                const file = e.target.files[0];
                if (!file) return;

                // 重置状态
                hideAllMessages();
                resetProgressPanel();
                showProgressPanel();

                // 初始化上传参数
                startTime = performance.now();
                totalFileSize = file.size;
                const formData = new FormData();
                formData.append('fileToUpload', file);

                try {
                    // 发起上传请求
                    const response = await fetch('/upload', {
                        method: 'POST',
                        body: formData,
                        credentials: 'same-origin',
                        onuploadprogress: (e) => {
                            if (e.lengthComputable) {
                                updateUploadProgress(e.loaded);
                            }
                        }
                    });

                    // 处理响应
                    if (response.ok) {
                        updateProgressPanel(100, '100% - 上传完成！', '#4CAF50');
                        showMessage(`文件 '${file.name}' 上传成功`, 'success');
                        setTimeout(() => {
                            hideProgressPanel();
                            refreshFileList(); // 刷新文件列表
                        }, 1500);
                    } else {
                        const errorHtml = await response.text();
                        const tempDiv = document.createElement('div');
                        tempDiv.innerHTML = errorHtml;
                        const errorText = tempDiv.querySelector('.error')?.textContent ||
                            `上传失败，服务器返回错误: ${response.status}`;
                        throw new Error(errorText);
                    }
                } catch (error) {
                    // 上传失败处理
                    updateProgressPanel(0, `上传失败：${error.message}`, '#dc3545');
                    showMessage(`文件 '${file.name}' 上传失败: ${error.message}`, 'error');
                } finally {
                    // 重置文件输入框，允许重新选择同一文件
                    fileInput.value = '';
                }
            });
        }

        // 2. 上传进度面板处理
        function initProgressPanelHandler() {
            const progressCloseBtn = document.getElementById('progressCloseBtn');
            progressCloseBtn.addEventListener('click', hideProgressPanel);
        }

        // 重置上传进度面板
        function resetProgressPanel() {
            const progressFill = document.getElementById('progressFill');
            const progressStats = document.getElementById('progressStats');
            progressFill.style.width = '0%';
            progressFill.style.backgroundColor = '#4CAF50';
            progressStats.textContent = '0% - 0 B/s - 剩余时间: 计算中...';
        }

        // 显示上传进度面板
        function showProgressPanel() {
            document.getElementById('progressPanel').style.display = 'block';
        }

        // 隐藏上传进度面板
        function hideProgressPanel() {
            document.getElementById('progressPanel').style.display = 'none';
        }

        // 更新上传进度面板
        function updateProgressPanel(percent, text, color) {
            const progressFill = document.getElementById('progressFill');
            const progressStats = document.getElementById('progressStats');
            progressFill.style.width = `${percent}%`;
            if (color) progressFill.style.backgroundColor = color;
            if (text) progressStats.textContent = text;
        }

        // 计算并更新上传进度
        function updateUploadProgress(loadedBytes) {
            // 计算进度百分比
            const percent = Math.round((loadedBytes / totalFileSize) * 100);

            // 计算上传速度
            const elapsedTime = (performance.now() - startTime) / 1000;
            const speedBps = elapsedTime > 0 ? loadedBytes / elapsedTime : 0;

            // 格式化速度单位
            let speed, unit;
            if (speedBps >= 1024 * 1024 * 1024) {
                speed = (speedBps / (1024 * 1024 * 1024)).toFixed(2);
                unit = 'GB/s';
            } else if (speedBps >= 1024 * 1024) {
                speed = (speedBps / (1024 * 1024)).toFixed(2);
                unit = 'MB/s';
            } else if (speedBps >= 1024) {
                speed = (speedBps / 1024).toFixed(2);
                unit = 'KB/s';
            } else {
                speed = speedBps.toFixed(0);
                unit = 'B/s';
            }

            // 计算剩余时间
            const remainingBytes = totalFileSize - loadedBytes;
            const etaSeconds = speedBps > 0 ? remainingBytes / speedBps : 0;
            const etaFormatted = formatEta(etaSeconds);

            // 更新UI
            updateProgressPanel(percent, `${percent}% - ${speed} ${unit} - 剩余时间: ${etaFormatted}`);
        }

        // 3. 删除功能处理（核心修复）
        function initDeleteHandler() {
            const fileList = document.getElementById('fileList');
            // 事件委托：监听所有删除按钮点击（兼容动态生成元素）
            fileList.addEventListener('click', async (e) => {
                // 只处理删除按钮
                if (!e.target.classList.contains('delete-btn')) return;

                // 阻止默认行为（兜底）
                e.preventDefault();
                e.stopPropagation();

                // 获取文件信息（从父元素dataset读取，避免编码问题）
                const li = e.target.closest('.file-item');
                const encodedFileName = li.dataset.filename;
                const originalFileName = li.dataset.originalname;

                // 确认删除（可选，提升用户体验）
                if (!confirm(`确定删除文件：${originalFileName}？`)) return;

                // 执行删除逻辑
                await handleFileDelete(encodedFileName, originalFileName);
            });
        }

        // 执行文件删除请求
        async function handleFileDelete(encodedFileName, originalFileName) {
            try {
                // 显示删除中提示
                showMessage(`正删除: ${originalFileName}`, 'success');
                console.log('[删除] 开始处理:', { encodedFileName, originalFileName });

                // 发送删除请求
                const response = await fetch(`/delete?file=${encodedFileName}`, {
                    method: 'GET',
                    credentials: 'include', // 携带Cookie
                    headers: {
                        'Accept': 'text/plain',
                        'Cache-Control': 'no-cache' // 禁用缓存
                    }
                });

                // 解析响应
                const responseText = await response.text();
                console.log('[删除] 服务器响应:', { status: response.status, text: responseText });

                // 处理响应状态
                if (response.ok) {
                    showMessage(`删除成功: ${originalFileName}`, 'success');
                    refreshFileList(); // 刷新文件列表
                } else {
                    throw new Error(`[${response.status}] ${responseText}`);
                }
            } catch (error) {
                console.error('[删除] 失败:', error);
                showMessage(`删除失败: ${originalFileName}（${error.message}）`, 'error');
            }
        }

        // 4. 搜索功能处理
        function initSearchHandler() {
            const searchInput = document.getElementById('searchInput');
            // 输入变化时实时过滤
            searchInput.addEventListener('input', renderFileList);
        }

        // 5. 排序功能处理
        function initSortHandler() {
            const sortSelect = document.getElementById('sortSelect');
            // 排序变化时重新渲染
            sortSelect.addEventListener('change', renderFileList);
        }

        // 刷新文件列表
        async function refreshFileList() {
            try {
                showMessage('加载文件列表中...', 'success');
                const response = await fetch('/files', {
                    cache: 'no-cache' // 禁用缓存
                });

                if (!response.ok) throw new Error(`HTTP错误: ${response.status}`);

                // 解析文件列表
                allFiles = await response.json();
                renderFileList(); // 渲染列表（自动应用排序/过滤）
                hideMessage('success');
            } catch (err) {
                showMessage(`获取文件列表失败: ${err.message}`, 'error');
                console.error('[刷新列表] 失败:', err);
            }
        }

        // 渲染文件列表（核心：包含排序、过滤、删除按钮渲染）
        function renderFileList() {
            const fileList = document.getElementById('fileList');
            const sortType = document.getElementById('sortSelect').value;
            const searchKeyword = document.getElementById('searchInput').value.toLowerCase().trim();

            // 1. 排序
            let processedFiles = sortFiles([...allFiles], sortType);
            // 2. 过滤
            if (searchKeyword) {
                processedFiles = processedFiles.filter(file =>
                    file.name.toLowerCase().includes(searchKeyword)
                );
            }

            // 3. 清空列表
            fileList.innerHTML = '';

            // 4. 无数据提示
            if (processedFiles.length === 0) {
                fileList.innerHTML = `<li class='empty-state'>没有匹配的文件</li>`;
                return;
            }

            // 5. 渲染文件项
            processedFiles.forEach(file => {
                const li = document.createElement('li');
                li.className = 'file-item';
                // 存储文件信息到dataset（供删除按钮使用）
                li.dataset.filename = encodeURIComponent(file.name);
                li.dataset.originalname = file.name;

                // 构建删除按钮（仅当URL包含#delete时显示）
                const deleteBtn = location.hash.includes('#delete')
                    ? `<a href='javascript:void(0);' class='download-btn delete-btn'>删除</a>`
                    : '';

                // 渲染文件项HTML
                li.innerHTML = `
                <span class='file-name'>${escapeHtml(file.name)}</span>
                <span class='file-meta'>
                    <span class='file-time'>${file.ctime || '未知时间'}</span>
                    <span class='file-size'>${formatSize(file.size)}</span>
                </span>
                ${deleteBtn}
                <a href='/download?file=${encodeURIComponent(file.name)}' class='download-btn' download>下载</a>
            `;

                fileList.appendChild(li);
            });
        }

        // 工具函数：文件排序
        function sortFiles(files, sortType) {
            return files.sort((a, b) => {
                const dateA = a.ctime ? new Date(a.ctime) : new Date(0);
                const dateB = b.ctime ? new Date(b.ctime) : new Date(0);

                switch (sortType) {
                    case 'time-desc': return dateB - dateA; // 最新
                    case 'time-asc': return dateA - dateB;  // 最旧
                    case 'size-desc': return b.size - a.size; // 最大
                    case 'size-asc': return a.size - b.size;  // 最小
                    default: return dateB - dateA;
                }
            });
        }

        // 工具函数：格式化文件大小
        function formatSize(bytes) {
            if (bytes < 1024) return `${bytes} B`;
            if (bytes < 1048576) return `${(bytes / 1024).toFixed(2)} KB`;
            if (bytes < 1073741824) return `${(bytes / 1048576).toFixed(2)} MB`;
            return `${(bytes / 1073741824).toFixed(2)} GB`;
        }

        // 工具函数：格式化剩余时间
        function formatEta(seconds) {
            if (isNaN(seconds) || seconds < 0) return '未知';
            const h = Math.floor(seconds / 3600);
            const m = Math.floor((seconds % 3600) / 60);
            const s = Math.floor(seconds % 60);
            if (h > 0) return `${h}时${m}分${s}秒`;
            if (m > 0) return `${m}分${s}秒`;
            return `${s}秒`;
        }

        // 工具函数：显示消息提示
        function showMessage(text, type) {
            const successEl = document.getElementById('successMessage');
            const errorEl = document.getElementById('errorMessage');

            hideAllMessages(); // 先隐藏所有提示

            if (type === 'success') {
                successEl.textContent = text;
                successEl.style.display = 'block';
                // 3秒后自动隐藏成功提示
                setTimeout(() => hideMessage('success'), 3000);
            } else if (type === 'error') {
                errorEl.textContent = text;
                errorEl.style.display = 'block';
            }
        }

        // 工具函数：隐藏指定类型消息
        function hideMessage(type) {
            if (type === 'success') {
                document.getElementById('successMessage').style.display = 'none';
            } else if (type === 'error') {
                document.getElementById('errorMessage').style.display = 'none';
            }
        }

        // 工具函数：隐藏所有消息
        function hideAllMessages() {
            hideMessage('success');
            hideMessage('error');
        }

        // 工具函数：HTML转义（防止XSS）
        function escapeHtml(unsafe) {
            if (!unsafe) return '';
            return unsafe
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/'/g, '&quot;')
                .replace(/'/g, '&#039;');
        }
    </script>

    <footer style='text-align: center; padding: 2rem; color: var(--secondary-color);'>
        <p>TestFileHttpServer © 2025 <a href='IAmSystem32@outlook.com'>I Am System32</a>, All rights reserved.</p>
    </footer>
</body>

</html>"
End Module
