Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.IO
Imports System.Web
Imports System.Xml.Serialization
Imports BusinessEntities

Public Class OrchardAPIClient

    Dim urlBase As String = "http://localhost/OrchardAPI/"
    Dim url As String
    Private client As HttpClient
    Dim response As System.Threading.Tasks.Task(Of HttpResponseMessage)
    Dim copyTask As Task
    Dim ds As DataSet
    Dim responseMessage As HttpResponseMessage
    Dim _APIException As Exception = Nothing
    Dim _APISuccessFlag As Boolean = True
    Dim ms As System.IO.MemoryStream
    Dim sr As StreamReader
    Dim _APIExceptionType As String
    Dim _APIExceptionCode As Integer
    Dim _APIExceptionMessage As String
    Dim util As Utilities
    Dim stringContent As System.Net.Http.StringContent
    Dim streamContent As System.Net.Http.StreamContent
    Dim responseContent As String
    Dim responseStream As System.IO.Stream
    Dim responseTask As System.Threading.Tasks.Task(Of HttpResponseMessage)
    Dim _serializer As Xml.Serialization.XmlSerializer

    Public Sub New()
        client = New HttpClient()
        client.BaseAddress = New Uri(urlBase)
        client.DefaultRequestHeaders.Accept.Clear()
        client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/xml"))

    End Sub

#Region "API Properties"
    Public ReadOnly Property APIExceptionType As String
        Get
            Return _APIExceptionType
        End Get
    End Property
    Public ReadOnly Property APIExceptionCode As Integer
        Get
            Return _APIExceptionCode
        End Get
    End Property
    Public ReadOnly Property APIExceptionMessage As String
        Get
            Return _APIExceptionMessage
        End Get
    End Property
    Public ReadOnly Property APIException As Exception
        Get
            Return _APIException
        End Get
    End Property
    Public ReadOnly Property SuccessFlag As Boolean
        Get
            Return _APISuccessFlag
        End Get
    End Property
#End Region

#Region "Private processing functions"
    Private Sub ProcessClientException(ex As Exception)
        'This routine will process an unexpected exception (like a timeout, or some
        'other unexpected condition).  Most errors from the WebAPI should be handled via
        'the isSuccessStatusCode property of the client.  THose errors are processed via
        'the ProcessClilentError routine

        _APIException = ex
        _APIExceptionMessage = ex.Message
        _APIExceptionCode = 0
        _APIExceptionType = ex.GetType.ToString
        _APISuccessFlag = False

    End Sub
    Private Sub ProcessClientError(ByVal errorContent As String)
        Dim message As String
        Dim msgParts() As String

        'Create an XML document to hold the information passed in the error string.
        'The string has the XML structure:
        '<Error><Message>Error message</Message?</Error>

        Dim apiError As New Xml.XmlDocument
        Dim docNode As Xml.XmlNode = apiError.CreateXmlDeclaration("1.0", "UTF-8", Nothing)
        apiError.AppendChild(docNode)
        Dim errorNode As Xml.XmlNode = apiError.CreateElement("Error")
        Dim errorAttribute As Xml.XmlAttribute = apiError.CreateAttribute("Message")
        errorNode.Attributes.Append(errorAttribute)
        apiError.AppendChild(errorNode)

        'Load the document with the errorContent
        apiError.LoadXml(errorContent)

        'Get the actual error message
        message = apiError.FirstChild.InnerText

        'Most exceptions returned from the WebAPI are in the format
        'errorType;errorCode;errorMessage
        'where errorType and errorMessage are strings and errorCode is integer
        '
        'Set the client flag to false
        _APISuccessFlag = False
        _APIException = Nothing

        'Attempt to split the message into respective parts
        msgParts = message.Split(";")

        'If there are 3 parts, then store the components in client properties
        If msgParts.Count = 3 Then
            _APIExceptionType = msgParts(0)
            _APIExceptionCode = msgParts(1)
            _APIExceptionMessage = msgParts(2)
        Else
            'If there were not three parts to the error message, then store the result
            'with an exception type of "Other" and an errorcode of 0
            _APIExceptionType = "Other"
            _APIExceptionCode = 0
            _APIExceptionMessage = message
        End If
    End Sub
    Private Function deserializeIntegerResponse(ByVal responseContent As String) As Integer
        Dim x As New Xml.Serialization.XmlSerializer(GetType(Integer))
        _APISuccessFlag = True
        ms = New IO.MemoryStream(ASCIIEncoding.Default.GetBytes(responseContent))
        ms.Position = 0
        Return x.Deserialize(ms)
    End Function
    Private Function deserializeBooleanResponse(ByVal responseContent As String) As Boolean
        Dim x As New Xml.Serialization.XmlSerializer(GetType(Boolean))
        _APISuccessFlag = True
        ms = New IO.MemoryStream(ASCIIEncoding.Default.GetBytes(responseContent))
        ms.Position = 0
        Return x.Deserialize(ms)
    End Function
    Private Function deserializeStringResponse(ByVal responseContent As String) As String
        Dim x As New Xml.Serialization.XmlSerializer(GetType(String))
        _APISuccessFlag = True
        ms = New IO.MemoryStream(ASCIIEncoding.Default.GetBytes(responseContent))
        ms.Position = 0
        Return x.Deserialize(ms)
    End Function
    Private Function MemoryStreamToString(ByVal _ms As MemoryStream) As String
        Dim enc As New UTF8Encoding
        Return enc.GetString(_ms.GetBuffer(), 0, _ms.Length)
    End Function
#End Region

#Region "**Irrigation Sets CRUD"
    Public Function GetIrrigationSetByPK(ByVal pk As Integer) As IrrigationSetEntity
        Dim _entity As New IrrigationSetEntity
        Try
            url = "IrrigationSets/IrrigationSet_pk/" + pk.ToString
            responseMessage = client.GetAsync(url).Result
            If responseMessage.IsSuccessStatusCode = True Then
                _entity = New Xml.Serialization.XmlSerializer(_entity.GetType).Deserialize(responseMessage.Content.ReadAsStreamAsync.Result)
                Return _entity
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
#End Region

#Region "*Irrigation Processes"
    'Declare codes for the functions of the WebAPI method "setOperations"
    Private Const const_openLogRecord = "1"
    Private Const const_turnOnSet = "2"

    Public Function turnOnSet(ByVal setPK As Integer) As Boolean
        Try
            url = "IrrigationFunctions/setOperations/" + setPK.ToString + "/" + const_turnOnSet
            stringContent = New System.Net.Http.StringContent("turnOnSet")
            responseTask = client.PutAsync(url, stringContent)
            responseTask.Wait()
            responseMessage = responseTask.Result
            If responseMessage.IsSuccessStatusCode = True Then
                responseContent = responseMessage.Content.ReadAsStringAsync.Result
                Return deserializeBooleanResponse(responseContent)
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
#End Region

#Region "*Pump Control"
    Public Function TurnPumpOn(ByVal setPK As Integer) As Boolean
        Try
            url = "PumpFunctions/TurnOn/" + setPK.ToString
            stringContent = New System.Net.Http.StringContent("TurnOnPump")
            responseTask = client.PutAsync(url, stringContent)
            responseTask.Wait()
            responseMessage = responseTask.Result
            If responseMessage.IsSuccessStatusCode = True Then
                responseContent = responseMessage.Content.ReadAsStringAsync.Result
                Return deserializeBooleanResponse(responseContent)
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try

    End Function
    Public Function TurnPumpOff() As Boolean

        Try
            url = "PumpFunctions/TurnOff/AllDone"
            stringContent = New System.Net.Http.StringContent("TurnOffPump")
            responseTask = client.PutAsync(url, stringContent)
            responseTask.Wait()
            responseMessage = responseTask.Result
            If responseMessage.IsSuccessStatusCode = True Then
                responseContent = responseMessage.Content.ReadAsStringAsync.Result
                Return deserializeBooleanResponse(responseContent)
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
    Public Function getPumpParameters() As BusinessEntities.pumpParameters
        Dim _pumpParameters As New BusinessEntities.pumpParameters
        Try

            url = "PumpFunctions/PumpParameters"
            responseTask = client.GetAsync(url)
            responseTask.Wait()
            responseMessage = responseTask.Result
            If responseMessage.IsSuccessStatusCode = True Then
                responseStream = responseMessage.Content.ReadAsStreamAsync.Result
                _pumpParameters = New Xml.Serialization.XmlSerializer(_pumpParameters.GetType).Deserialize(responseStream)
                Return _pumpParameters
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try

    End Function

#End Region

#Region "*Relay Control"
    
    Public Function getRelayValveView() As List(Of BusinessEntities.RelayValveViewEntity)
        Dim _entitys As New List(Of BusinessEntities.RelayValveViewEntity)
        Try
            url = "RelayFunctions/relayValveViewALL/"
            responseMessage = client.GetAsync(url).Result
            If responseMessage.IsSuccessStatusCode = True Then
                _entitys = New Xml.Serialization.XmlSerializer(_entitys.GetType).Deserialize(responseMessage.Content.ReadAsStreamAsync.Result)
                Return _entitys
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
    Public Function getRelayValveViewByRelay(ByVal relayPK As Integer) As BusinessEntities.RelayValveViewEntity
        Dim _entity As New BusinessEntities.RelayValveViewEntity
        Try
            url = "RelayFunctions/relayValveViewByRelay/" + relayPK.ToString
            responseMessage = client.GetAsync(url).Result
            If responseMessage.IsSuccessStatusCode = True Then
                _entity = New Xml.Serialization.XmlSerializer(_entity.GetType).Deserialize(responseMessage.Content.ReadAsStreamAsync.Result)
                Return _entity
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
    Public Function closeAllRelayValves() As Integer
        Try
            url = "RelayFunctions/closeAllRelayValves/"
            stringContent = New System.Net.Http.StringContent("closeAllRelayValves")
            responseTask = client.PutAsync(url, stringContent)
            responseTask.Wait()
            responseMessage = responseTask.Result
            If responseMessage.IsSuccessStatusCode = True Then
                Return deserializeIntegerResponse(responseMessage.Content.ReadAsStringAsync.Result)
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
    Public Function SetRelayStatus(ByVal relayIP As String, ByVal bank As Byte, ByVal channel As Byte, ByVal status As Integer) As Boolean
        Try
            url = "RelayFunctions/SetRelayStatus/" + relayIP + "/" + bank.ToString + "/" + channel.ToString + "/" + status.ToString

            stringContent = New System.Net.Http.StringContent("Set channel status")
            responseTask = client.PutAsync(url, stringContent)
            responseTask.Wait()
            responseMessage = responseTask.Result

            If responseMessage.IsSuccessStatusCode = True Then
                Return deserializeBooleanResponse(responseMessage.Content.ReadAsStringAsync.Result)
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function

    Public Function ActivateRelayWithTimer(ByVal relayIP As String, bank As Byte, channel As Byte, timer As Byte, minutes As Integer) As Boolean
        Try
            url = "RelayFunctions/ActivateRelayWithTimer/" + relayIP + "/" + bank.ToString + "/" + channel.ToString + _
                    "/" + timer.ToString + "/" + minutes.ToString
            stringContent = New System.Net.Http.StringContent("Activate Relay With Timer")

            responseTask = client.PutAsync(url, stringContent)
            responseTask.Wait()
            responseMessage = responseTask.Result

            If responseMessage.IsSuccessStatusCode = True Then
                Return deserializeBooleanResponse(responseMessage.Content.ReadAsStringAsync.Result)
            Else
                ProcessClientError(responseMessage.Content.ReadAsStringAsync.Result)
                Return Nothing
            End If
        Catch ex As Exception
            ProcessClientException(ex)
            Return Nothing
        End Try
    End Function
    
#End Region

End Class
