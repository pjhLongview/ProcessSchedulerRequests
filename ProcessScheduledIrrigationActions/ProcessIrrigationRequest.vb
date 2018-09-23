Imports BusinessEntities
Imports System.IO
Imports System.Text
Imports System.Threading
Public Class ProcessIrrigationRequest
    Dim dateTimeStamp As String = Now.ToShortDateString + " " + Now.ToShortTimeString + " "
    Dim fs As System.IO.StreamWriter
    Dim ctl As New OrchardAPIClient()
    Dim pumpParameters As New BusinessEntities.pumpParameters()

    Private Sub ProcessIrrigationRequest_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load

        'This program is instantiated by the Windows Task Scheduler to process various Irrigation Systsem actions.
        'The program is instantiated with command line arguments, which direct the actions.
        'Command line arguments are defined as:
        '/action=<actioncode>

        '<actioncode> is defined as:
        '1=select set, turn set on, turn pump on
        '2=switch currently selected set to a new set
        '3=turn pump off, close all valves
        '4=diagnostic. Echo all command line switches to log file
        '5=initialize and activate a relay with the specified duration timer

        '/setPK=<irrigation set primary key>
        '/TimerID=<timer id on the relay board(0-9)>
        '/relayPK=<relay key in the viewrelay tabl>
        '/timerMinutes=<minutes the relay should be on>
        '
        '
        'Each action is logged to a log file stored in \windows\logs\orchardAutomation\scheduledTasks.log
        '


        Dim args As String() = Environment.GetCommandLineArgs()
        Dim setTurnedOn As Boolean
        Dim pumpTurnedOn As Boolean
        Dim pumpTurnedOff As Boolean
        Dim keyPair() As String
        Dim nValves As Integer
        Dim logfile As String = "C:\Users\pjhunter\Documents\Ranch\Automated Irrigation\OrchardAutomation\schduledTasks.log"
        Dim action As Integer
        Dim setPK As Integer = -1
        Dim relayIP As String = ""
        Dim timerID As Byte
        Dim relayPK As Integer = -1
        Dim timerMinutes As Integer
        Dim timerHours As Integer = 0
        Dim badParse As Boolean = False
        Dim _irrigationSet As New BusinessEntities.IrrigationSetEntity()
        Dim _relayValveView As New BusinessEntities.RelayValveViewEntity()
        Dim PSIOK As Boolean = True


        fs = New System.IO.StreamWriter(logfile, True)

        If args.Length = 1 Then
            recordErrorInLog("No arguments found on command line. Terminating.")
         End If

        'Process each of the arguments and get the relevant information.
        For i = 1 To args.Length - 1
            keyPair = args(i).Split("=")
            If (keyPair(0).ToUpper = "/ACTION") Then
                If Integer.TryParse(keyPair(1), action) = False Then
                    recordErrorInLog("Invalid /action code:" + keyPair(1) + ". Must be numeric.")
                 End If
            End If
            If (keyPair(0).ToUpper = "/SETPK") Then
                If Integer.TryParse(keyPair(1).Trim, setPK) = False Then
                    recordErrorInLog("Invalid /setPK value:" + keyPair(1) + ". Must be numeric.")
                End If
            End If
            If (keyPair(0).ToUpper = "/TIMERID") Then
                If Byte.TryParse(keyPair(1), timerID) = False Then
                    recordErrorInLog("Invalid /timerID value:" + keyPair(1) + ". Must be numeric between 1 and 10.")
                End If
            End If
            If (keyPair(0).ToUpper = "/RELAYPK") Then
                If Integer.TryParse(keyPair(1), relayPK) = False Then
                    recordErrorInLog("Invalid /relayPK value:" + keyPair(1) + ". Must be numeric.")
                End If
            End If
            If (keyPair(0).ToUpper = "/TIMERMINUTES") Then
                If Integer.TryParse(keyPair(1), timerMinutes) = False Then
                    recordErrorInLog("Invalid /timerMinutes value:" + keyPair(1) + ". Must be numeric.")
                End If
            End If

        Next

        'Process the requested action:
        Select Case action

            Case 1  'Select a set, turn it on then turn the pump on
                _irrigationSet = ctl.GetIrrigationSetByPK(setPK)
                If (_irrigationSet Is Nothing) Then
                    recordErrorInLog("Irrigation set having primary key of " + setPK.ToString + " not found. Terminating.")
                End If
                fs.WriteLine(dateTimeStamp + "Request: Select " + _irrigationSet.irrigationSet_id + ", turn it on and then turn on pump.")
                Try
                    setTurnedOn = ctl.turnOnSet(setPK)
                    If setTurnedOn = False Then
                        recordErrorInLog("Error turning on set from API. Details:" + ctl.APIExceptionMessage)
                    End If
                Catch ex As Exception
                    recordErrorInLog("Error turning on set " + _irrigationSet.irrigationSet_id + ". Details:" + ex.Message)
                End Try

                If (setTurnedOn = True) Then
                    Try
                        'Get the current pump parameters so as to get the current PSI.  It must be below 5PSI in order to turn the pump on
                        pumpParameters = ctl.getPumpParameters()
                        PSIOK = (pumpParameters.currentPSI < pumpParameters.startPSI)
                        'if the current PSI is too high, then call a routine that will wait for the PSI to
                        'fall.
                        If PSIOK = False Then
                            PSIOK = waitForPSI()
                        End If

                        'If the PSI has not fallen to an acceptable level, abort the attempt
                        If PSIOK = False Then
                            recordErrorInLog("Error: The current PSI (" + pumpParameters.currentPSI.ToString + _
                                             ") remains higher than the maximum allowed for the pump to turn on (" + _
                                             pumpParameters.startPSI.ToString + ").  Terminating request.")
                        End If
                        pumpTurnedOn = ctl.TurnPumpOn(setPK)
                    Catch ex As Exception
                        recordErrorInLog("Error turning on pump. Details:" + ex.Message)
                    End Try
                Else
                    recordErrorInLog("Set " + _irrigationSet.irrigationSet_id + " failed to turn on properly. Termininting withou starting pump")
                End If
                If pumpTurnedOn = True Then
                    fs.WriteLine(dateTimeStamp + "Pump successfully turned on.")
                Else
                    recordErrorInLog("Set turned on, but pump failed to turn on. Details: " + ctl.APIExceptionMessage)
                End If

            Case 2  'Change irrigation sets
                _irrigationSet = ctl.GetIrrigationSetByPK(setPK)
                If (_irrigationSet Is Nothing) Then
                    recordErrorInLog("Irrigation set having primary key of " + setPK.ToString + " not found. Terminating.")
                End If
                fs.WriteLine(dateTimeStamp + "Request: Close current irrigation set and turn on set " + _irrigationSet.irrigationSet_id)

                setTurnedOn = ctl.turnOnSet(setPK)
                If setTurnedOn = True Then
                    fs.WriteLine(dateTimeStamp + _irrigationSet.irrigationSet_id + " successfully turned on.")
                Else
                    recordErrorInLog("Unable to turn on selected set " + _irrigationSet.irrigationSet_id + " Pump is still on. Details:" + ctl.APIExceptionMessage)
                End If

            Case 3  'Turn pump off, close all valves
                Try
                    fs.WriteLine(dateTimeStamp + "Request: Turn off pump. Close all valves.")
                    pumpTurnedOff = ctl.TurnPumpOff()
                    If pumpTurnedOff = True Then
                        Try
                            nValves = ctl.closeAllRelayValves()
                            fs.WriteLine(dateTimeStamp + nValves.ToString() + " valve(s) successfully turned off.")
                        Catch ex As Exception
                            recordErrorInLog("Error turning off valves. Details:" + ex.Message)
                        End Try
                    End If
                Catch ex As Exception
                    recordErrorInLog("Error turning off pump. Details:" + ex.Message)
                End Try

            Case 4  'Diagnostic case.  Echo all the command lines arguments
                Dim sb As New System.Text.StringBuilder
                For i = 1 To args.Length - 1
                    sb.Append(args(i) + " ")
                Next
                fs.WriteLine(dateTimeStamp + "Request: Argument test: " + sb.ToString)

            Case 5  'Initiate relay with timer
                'The TimerRelayID is the actual channel number on the board, that must be broken down into board, channel on board.
                Dim bank As Byte
                Dim channel As Byte
                Dim timerActivated As Boolean
                Try
                    fs.WriteLine(dateTimeStamp + "Request: Activate relay for a specific duration via timer.")
                    _relayValveView = ctl.getRelayValveViewByRelay(relayPK)
                    bank = _relayValveView.relayValve_bank
                    channel = _relayValveView.relayValve_relayChannel
                    relayIP = _relayValveView.relayValve_relay_ip
                    timerActivated = ctl.ActivateRelayWithTimer(relayIP, bank, channel, timerID, timerMinutes)
                    If timerActivated = False Then
                        recordErrorInLog("Error turning on channel timer.")
                    Else
                        fs.WriteLine(dateTimeStamp + "Successfully started timer " + timerID.ToString + " on board " + relayIP + " bank " + _
                                     _relayValveView.relayValve_bank.ToString + " channel " + _relayValveView.relayValve_relayChannel.ToString + _
                                     " (" + _relayValveView.relayValve_dripValve_id + ")" + " for " + timerMinutes.ToString + " minutes.")

                    End If
                Catch ex As Exception
                    recordErrorInLog("Error activating channel timer. Details:" + ex.Message)
                End Try

            Case Else
                recordErrorInLog("Invalid action code=" + action.ToString)
        End Select

        fs.Close()
        Environment.Exit(0)
    End Sub
    Private Sub recordErrorInLog(ByVal msg As String)
        fs.WriteLine(dateTimeStamp + msg)
        fs.Flush()
        fs.Close()
        Environment.Exit(1)
    End Sub
    Private Function waitForPSI() As Boolean
        Dim lastPSI As Decimal
        Dim nCycles As Integer = 0
        Dim PSIok As Boolean
        Dim deltaPSI As Decimal
        Dim estCycles As Integer
        'Dim relayValveViewTable As List(Of BusinessEntities.RelayValveViewEntity)
        'Dim relayValveView As BusinessEntities.RelayValveViewEntity
        'Dim query As Object
        lastPSI = pumpParameters.currentPSI

        While nCycles <= 10
            'Wait 30 seconds for the pressure to fall
            Threading.Thread.Sleep(60000)
            nCycles = nCycles + 1
            dateTimeStamp = Now.ToShortDateString + " " + Now.ToShortTimeString + " "
            pumpParameters = ctl.getPumpParameters()
            PSIok = (pumpParameters.currentPSI < pumpParameters.startPSI)
            If PSIok = True Then
                fs.WriteLine(dateTimeStamp + "After " + nCycles.ToString + " cycles, PSI dropped to " + pumpParameters.currentPSI.ToString + _
                             ".  Ok to start pump")
                Return True
            Else
                fs.WriteLine(dateTimeStamp + "After " + nCycles.ToString + " current pressure at " + pumpParameters.currentPSI.ToString)

                deltaPSI = lastPSI - pumpParameters.currentPSI
                If deltaPSI > 0 Then
                    estCycles = Math.Truncate((pumpParameters.currentPSI - pumpParameters.startPSI) / deltaPSI)
                    fs.WriteLine(dateTimeStamp + "Estimated cycles to start PSI is " + estCycles.ToString)
                    'If estCycles > 10 Then
                    '    'need to open a downstream circuit
                    '    'Find a circuit that is not currently open in either the TAD or TOBY orchards (both downstream)
                    '    relayValveViewTable = ctl.getRelayValveView
                    '    query = From x In relayValveViewTable.AsEnumerable Where x.relayValve_relayChannelStatus = 0 _
                    '           And x.relayValve_dripValve_id Like "TAD*" Or x.relayValve_dripValve_id Like "TOBY*" _
                    '           Select x.relayValve_pk

                    '    'Turn on each relay. That should drop the pressure!
                    '    For Each item In query

                    '    Next()
                    'End If
                End If
                lastPSI = pumpParameters.currentPSI
            End If
        End While
        'After 10 minutes, the pressure did not drop sufficiently, so give up.
        Return False
    End Function
End Class
