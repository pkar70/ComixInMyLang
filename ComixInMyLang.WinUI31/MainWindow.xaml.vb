Option Strict On

Imports System.Collections.ObjectModel
Imports System.IO
'Imports System.Reflection.Metadata
'Imports System.Runtime.CompilerServices
'Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.UI
Imports Microsoft.UI.Xaml
Imports Microsoft.UI.Xaml.Media.Imaging
'Imports Windows.ApplicationModel.DataTransfer
'Imports Windows.Foundation

Public Class MainWindow
    Inherits Window

    Private ReadOnly _backdrop As BackdropHelper

    Sub New()

        Title = "ComixInMyLang"

        InitializeComponent()

        _backdrop = New BackdropHelper(Me)
        Dim useAcrylic = False
        _backdrop.SetBackdrop(BackdropType.Mica, useAcrylic)

        TryCustomizeTitleBar(useAcrylic)


    End Sub

    Private Sub TryCustomizeTitleBar(useAcrylic As Boolean)
        Dim appWnd = GetAppWindow
        Dim titleBar = appWnd.TitleBar
        If titleBar IsNot Nothing Then
            ' Windows 11 or Windows 10
            With appWnd.TitleBar
                .ExtendsContentIntoTitleBar = True
                .ButtonBackgroundColor = Colors.Transparent
            End With
            Dim titleBarHeight = appWnd.GetTitleBarHeight

            uiGrid.RowDefinitions(0).Height = New GridLength(titleBarHeight + 4, GridUnitType.Pixel)
            If Not useAcrylic Then
                uiGrid.Background = Nothing
                '    ConvertingFiles.Background = Nothing
            End If
        Else
            ' Windows 10
            'TblTitleText.Visibility = Visibility.Collapsed
        End If
    End Sub

    Private _loaded As Boolean
    Private Sub MainWindow_Activated(sender As Object, args As WindowActivatedEventArgs) Handles Me.Activated
        WinUIVbHost.Instance.CurrentWindow = Me
    End Sub

    Private moBmp As BitmapImage = Nothing
    Private moSoftBmp As Windows.Graphics.Imaging.SoftwareBitmap = Nothing
    Private mlTeksty As Collection(Of JedenText) = Nothing
    Private msCurrFile As String = ""
    Private moFiles As Collection(Of Windows.Storage.StorageFile)
    Private msFolder As String

    Private Async Function PokazObrazek(oFile As Windows.Storage.StorageFile) As Task
        ' pokaz obrazek
        moBmp = New BitmapImage
        Dim oStream As Stream = Await oFile.OpenStreamForReadAsync
        Dim oStrRA = oStream.AsRandomAccessStream
        Await moBmp.SetSourceAsync(oStrRA)
        uiPic.Source = moBmp
        uiPic.Width = uiGrid.ActualWidth - 10

        Page_SizeChanged(Nothing, Nothing)

        ' zrob z niego SoftBmp
        oStream.Seek(0, SeekOrigin.Begin)
        Dim oDecoder As Windows.Graphics.Imaging.BitmapDecoder = Await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(oStream.AsRandomAccessStream)
        moSoftBmp = Await oDecoder.GetSoftwareBitmapAsync()

        oStream.Dispose()

    End Function

    Private Async Sub uiOpenFile_Click(sender As Object, e As RoutedEventArgs)
        ' browse file
        Dim oFile As Windows.Storage.StorageFile
        oFile = Await BrowseFile()
        If oFile Is Nothing Then Return

        Dim fileExt As String = oFile.FileType.ToLowerInvariant

        If fileExt = ".cbr" OrElse fileExt = ".cbz" Then
            Dim oArchive = SharpCompress.Readers.ReaderFactory.Open(oFile.OpenStreamForReadAsync.Result)
            While oArchive.MoveToNextEntry
                If oArchive.Entry.IsDirectory Then Continue While

                'Dim oNew As JedenText = New JedenText
                'oNew.sFileName = oArchive.Entry.Key
                'oNew.sOriginal = vbCrLf & vbCrLf & vbCrLf & "File: " & oArchive.Entry.Key & vbCrLf & vbCrLf
                'If mlTeksty Is Nothing Then mlTeksty = New Collection(Of JedenText)
                'mlTeksty.Add(oNew)
                msCurrFile = oArchive.Entry.Key  'oFile.Name

                Try
                    Dim oStream = oArchive.OpenEntryStream()
                    Dim oMemStream As MemoryStream = New MemoryStream
                    oStream.CopyTo(oMemStream)

                    oMemStream.Seek(0, SeekOrigin.Begin)
                    Dim oDecoder As Windows.Graphics.Imaging.BitmapDecoder
                    oDecoder = Await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(oMemStream.AsRandomAccessStream)
                    moSoftBmp = Await oDecoder.GetSoftwareBitmapAsync()

                    oStream.Dispose()
                    oMemStream.Dispose()

                    Await ZrobOCR()

                Catch ex As Exception
                    Exit While
                End Try

            End While

        Else
            msCurrFile = oFile.Name
            msFolder = oFile.Path

            'uiNext.IsEnabled = False
            'uiPrev.IsEnabled = False

            Await PokazObrazek(oFile)

            ' ocr file
            Await ZrobOCR()
        End If

    End Sub



    Private Async Function BrowseFile() As Task(Of Windows.Storage.StorageFile)
        ' FileOpenPicker  , potem FolderPicker 
        Dim oPicker As New Windows.Storage.Pickers.FileOpenPicker

        oPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail
        oPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        oPicker.FileTypeFilter.Add(".jpg")
        oPicker.FileTypeFilter.Add(".jpeg")
        oPicker.FileTypeFilter.Add(".png")
        oPicker.FileTypeFilter.Add(".cbr")
        oPicker.FileTypeFilter.Add(".cbz")
        ' https://github.com/microsoft/WindowsAppSDK/issues/1188
        Dim hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Me)
        WinRT.Interop.InitializeWithWindow.Initialize(oPicker, hwnd)

        Dim oFile As Windows.Storage.StorageFile
        oFile = Await oPicker.PickSingleFileAsync

        Return oFile
    End Function

    Private Async Function BrowseFolder() As Task(Of Windows.Storage.StorageFolder)
        ' FileOpenPicker  , potem FolderPicker 
        Dim oPicker As Windows.Storage.Pickers.FolderPicker = New Windows.Storage.Pickers.FolderPicker

        ' oPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail
        oPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        oPicker.FileTypeFilter.Add(".jpg")

        Dim oFolder As Windows.Storage.StorageFolder
        oFolder = Await oPicker.PickSingleFolderAsync

        Return oFolder
    End Function

    Private Async Function ZrobOCR() As Task(Of Boolean)
        If mlTeksty Is Nothing Then mlTeksty = New Collection(Of JedenText)

        Dim rOCR As Windows.Media.Ocr.OcrResult = Await Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages().RecognizeAsync(moSoftBmp)

        Dim sInput As String = uiTextOrg.Text
        If rOCR.Lines.Count < 1 Then Return False

        Dim oNew As JedenText = New JedenText
        oNew.sFileName = msCurrFile
        oNew.sOriginal = vbCrLf & vbCrLf & vbCrLf & vbCrLf & vbCrLf & msCurrFile & vbCrLf & vbCrLf
        mlTeksty.Add(oNew)

        sInput = sInput & oNew.sOriginal

        For Each oLine As Windows.Media.Ocr.OcrLine In rOCR.Lines
            oNew = New JedenText
            oNew.sFileName = msCurrFile
            oNew.sOriginal = oLine.Text.ToLower
            mlTeksty.Add(oNew)
            sInput = sInput & vbCrLf & oNew.sOriginal
        Next

        uiTextOrg.Text = sInput
        ' clipboard copy
        Return True
    End Function

    'tak tego teraz nie używam, a może Newtonsoft byłby niepotrzebny?

    'Private Function TranslateText(sInput As String) As String
    '    ' https://docs.microsoft.com/en-us/azure/cognitive-services/translator/quickstart-csharp-translate
    '    Dim sHost As String = "api-eur.cognitive.microsofttranslator.com"   ' europejskie
    '    Dim sRoute As String = "/translate?api-version=3.0&to=pl"

    '    Dim subscriptionKey As String = "YOUR_SUBSCRIPTION_KEY"

    '    'System.Object[] body = New System.Object[] { New { Text = @"Hello world!" } };
    '    Dim oRequestBody As String = Newtonsoft.Json.JsonConvert.SerializeObject(sInput)
    '    Dim oClient As Net.Http.HttpClient = New Net.Http.HttpClient()
    '    Dim oRequest As Net.Http.HttpRequestMessage = New Net.Http.HttpRequestMessage()

    '    oRequest.Method = Net.Http.HttpMethod.Post
    '    oRequest.RequestUri = New Uri(sHost + sRoute)
    '    oRequest.Content = New Net.Http.StringContent(oRequestBody, System.Text.Encoding.UTF8, "application/json")
    '    oRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey)

    '    Dim oResponse As Net.Http.HttpResponseMessage = oClient.SendAsync(oRequest).Result
    '    Dim sJsonResponse As String = oResponse.Content.ReadAsStringAsync().Result

    '    Dim oJsonResp As Windows.Data.Json.JsonValue = Windows.Data.Json.JsonValue.Parse(sJsonResponse)
    '    Dim oJsonTrans As Windows.Data.Json.JsonArray = oJsonResp.GetObject().GetNamedArray("translations")
    '    Dim oJsonLang As Windows.Data.Json.JsonValue = oJsonTrans.Item(0)
    '    Dim sTranslat As String = oJsonLang.GetObject.GetNamedString("text")

    '    Return sTranslat

    'End Function

    Private Async Function DoTranslation() As Task(Of Boolean)
        For Each oLine As JedenText In mlTeksty
            ' przetlumacz oLine.sOriginal -> oLine.sTranslation
        Next
        ' tlumaczenie w jednym call, ale z rozbiciem pozniej na kawalki?
    End Function

    Private Async Sub uiOpenFolder_Click(sender As Object, e As RoutedEventArgs)
        Dim oFolder As Windows.Storage.StorageFolder
        oFolder = Await BrowseFolder()
        'Dim oPicker As Windows.Storage.Pickers.FolderPicker = New Windows.Storage.Pickers.FolderPicker

        ''oPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail
        'oPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary

        'Dim oFolder As Windows.Storage.StorageFolder
        'oFolder = Await oPicker.PickSingleFolderAsync

        If oFolder Is Nothing Then Return

        uiNext.IsEnabled = True
        uiPrev.IsEnabled = True
        moFiles = New Collection(Of Windows.Storage.StorageFile)

        ' przejdz przez wszystkie pliki
        Try
            For Each oFile As Windows.Storage.StorageFile In Await oFolder.GetFilesAsync

                Dim bFileOk As Boolean = False
                If oFile.Name.EndsWith("jpg") Then bFileOk = True
                If oFile.Name.EndsWith("png") Then bFileOk = True
                If Not bFileOk Then Continue For

                msCurrFile = oFile.Name
                moFiles.Add(oFile)

                ' pokaz obrazek
                Dim oStream As Stream = Await oFile.OpenStreamForReadAsync

                ' zrob z niego SoftBmp
                Dim oDecoder As Windows.Graphics.Imaging.BitmapDecoder = Await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(oStream.AsRandomAccessStream)
                moSoftBmp = Await oDecoder.GetSoftwareBitmapAsync()

                oStream.Dispose()

                ' ocr file
                Await ZrobOCR()

            Next

            ' właduj pierwsze
            msCurrFile = ""
            uiNext_Click(Nothing, Nothing)
            Return
        Catch ex As Exception
            MessageBox.Show("@uiOpenFolder_Click" & ex.Message)
        End Try

    End Sub

    Private Async Sub uiPrev_Click(sender As Object, e As RoutedEventArgs) Handles uiPrev.Click
        Dim oFile As Windows.Storage.StorageFile = Nothing

        If msCurrFile = "" Then Return

        If moFiles.Count < 1 Then
            ' czyli spróbuj wedle nazwy
            Dim iLen As Integer = msCurrFile.Length
            Dim iPicNo As Integer
            If Not Integer.TryParse(msCurrFile.Substring(iLen - 7, 3), iPicNo) Then Return
            iPicNo -= 1
            If iPicNo < 0 Then Return
            Dim sFile As String = ""
            If iLen > 7 Then sFile = msCurrFile.Substring(0, iLen - 7)
            sFile = sFile & iPicNo.ToString("000")
            sFile = sFile & msCurrFile.Substring(iLen - 4)

            ' prawdopodobnie mamy nazwę pliku.
            'Await Vblib.DialogBoxAsync("New file name: " & sFile)
            MessageBox.Show("New file name: " & sFile)

        Else
            For iFor As Integer = 1 To moFiles.Count - 1
                If moFiles.Item(iFor).Name = msCurrFile Then
                    oFile = moFiles.Item(iFor - 1)
                End If
            Next
        End If

        If oFile Is Nothing Then Return

        Await PokazObrazek(oFile)

    End Sub

    Private Async Sub uiNext_Click(sender As Object, e As RoutedEventArgs) Handles uiNext.Click
        Dim oFile As Windows.Storage.StorageFile = Nothing

        If moFiles.Count < 1 Then
            ' czyli spróbuj wedle nazwy
        Else

            If msCurrFile = "" Then
                oFile = moFiles.Item(0)
            Else
                For iFor As Integer = 0 To moFiles.Count - 2
                    If moFiles.Item(iFor).Name = msCurrFile Then
                        oFile = moFiles.Item(iFor + 1)
                    End If
                Next
            End If
        End If

        If oFile Is Nothing Then Return

        Await PokazObrazek(oFile)

    End Sub

    Private Sub Page_SizeChanged(sender As Object, e As WindowSizeChangedEventArgs)
        ' ustaw zoom
        If moBmp Is Nothing Then Return

        Dim dZoom As Single = Convert.ToSingle(uiGrid.ActualWidth / moBmp.PixelWidth)
        uiScroll.ChangeView(Nothing, Nothing, dZoom)
    End Sub


    Private Sub uiTranslate_Click(sender As Object, e As RoutedEventArgs)

    End Sub
End Class

Public Class JedenText
    Public Property sFileName As String
    Public Property sOriginal As String
    Public Property sTranslation As String
End Class
