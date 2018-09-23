Imports System.Text

Public Class Utilities
    Dim ctl As OrchardAPIClient
    Public ReadOnly Property GallonsPerAcreFoot As Decimal
        Get
            Return 325851.428571
        End Get
    End Property
    Public Sub New(ByRef _ctl As OrchardAPIClient)
        ctl = _ctl

    End Sub
    Private Function initChannelMasks() As Byte()
        Dim channelMask(7) As Byte
        channelMask(0) = &H1
        channelMask(1) = &H2
        channelMask(2) = &H4
        channelMask(3) = &H8
        channelMask(4) = &H10
        channelMask(5) = &H20
        channelMask(6) = &H40
        channelMask(7) = &H80
        Return channelMask
    End Function

End Class
