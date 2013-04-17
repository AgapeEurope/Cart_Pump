Public Module DestinationType
    Public Const Staff As Integer = 0
    Public Const Department As Integer = 1
    Public Const Project As Integer = 2
    Public Function GetName(ByVal DestinationTypeNumber As Integer) As String
        Select Case DestinationTypeNumber
            Case 0 : Return "Staff"
            Case 1 : Return "Department"
            Case 2 : Return "Project"
            Case Else : Return "Unknown"
        End Select
    End Function

End Module
Public Module OrderState
    Public Const Unprocessed As Integer = 0 'a cart is created but before user is at payment screen
    Public Const Submitted As Integer = 1 'Credit order submitted but before payment is received. If payment goes through credit card : status before we get confirmation from credit card company
    Public Const Completed As Integer = 2
    Public Const Canceled As Integer = 3 'order not shipped out or completed
    Public Const Returned As Integer = 4
    Public Const Downloading As Integer = 5
    Public Const Downlaoded As Integer = 6
    Public Const ErrorDownloading As Integer = 7

End Module
Public Module ItemType
    Public Const Donation As Integer = 0
    Public Const Resource As Integer = 1
    Public Function GetName(ByVal ItemTypeNumber As Integer) As String
        Select Case ItemTypeNumber
            Case 0 : Return "Donation"
            Case 1 : Return "Resource"
            Case Else : Return "Unknown"
        End Select
    End Function

End Module

Module Module1
    Dim api As New FCX_API.FCX_API()
    Dim api_key = New System.Guid()


    Sub Main()
        api.Url = "http://dev.agapefrance.org/FCX_API/FCX-API.asmx"
        api.Discover()
        ProcessDonations()





    End Sub
    Private Function ZeroFill(ByVal number As Integer, ByVal len As Integer) As String
        If number.ToString.Length > len Then
            Return Right(number.ToString, len)
        Else
            Dim Filler As String = ""
            For i As Integer = 1 To len - number.ToString.Length
                Filler &= "0"
            Next
            Return Filler & number.ToString
        End If


    End Function

    Private Sub ProcessDonations()
        Dim d As New DonationDataContext
        Try
            api_key = New System.Guid(d.AP_StaffBroker_Settings.Where(Function(x) x.PortalId = 0 And x.SettingName = "Cart_Pump_Key").Select(Function(x) x.SettingValue).First)

        Catch ex As Exception
            Console.Write("No Key!")
            Return
        End Try
        



        'generate the next batch number
        Dim nr = d.AP_StaffBroker_Settings.Where(Function(x) x.PortalId = 0 And x.SettingName = "NextBatchNo")
        Dim NewBatchRef As Integer = 1
        If nr.Count = 0 Then
            Dim i As New AP_StaffBroker_Setting
            i.SettingName = "NextBatchNo"
            i.SettingValue = 1
            i.PortalId = 0
            d.AP_StaffBroker_Settings.InsertOnSubmit(i)

        Else
            NewBatchRef = nr.First.SettingValue
            nr.First.SettingValue += 1

        End If
        d.SubmitChanges()
        'If d.FR_Donations.Where(Function(x) Not x.BatchID Is Nothing).Count() > 0 Then
        ' NewBatchRef = d.FR_Donations.Where(Function(x) Not x.BatchID Is Nothing).Max(Function(x) x.BatchID)

        'End If


        'get all completed credit card donations, which are ready for download
        Dim q = From c In d.FR_Cart_Contents Where c.FR_Cart.OrderState = OrderState.Completed And c.FR_Cart.PortalID = 0 And c.ItemType = ItemType.Donation
        Dim UniqueRef = "CC" & ZeroFill(NewBatchRef, 6)
        Dim donations As New List(Of FCX_API.Donation)
        For Each row In q
            Dim insert As New FCX_API.Donation
            insert.Amount = row.Cost
            insert.PaymentType = row.FR_Cart.PayMethod

            If row.FR_Donations.First.DestinationType = DestinationType.Staff Then
                'staff
                Dim staff = From c In d.AP_StaffBroker_Staffs Where c.StaffId = row.FR_Donations.First.DestinationID

                If staff.Count > 0 Then
                    insert.DesigId = staff.First.AP_StaffBroker_StaffProfiles.Where(Function(x) x.AP_StaffBroker_StaffPropertyDefinition.FixedFieldName = "Designation").Select(Function(x) x.PropertyValue).First
                End If
            Else
                'Departemnt or Project
                Dim dept = From c In d.AP_StaffBroker_Departments Where c.CostCenterId = row.FR_Donations.First.DestinationID

                If dept.Count > 0 Then
                    insert.DesigId = dept.First.PayType
                End If


            End If

            Dim donor As New FCX_API.Donor
            donor.City = getProfileProperty(row.FR_Cart.User, "City")
            donor.Country = getProfileProperty(row.FR_Cart.User, "Country")
            donor.Email = row.FR_Cart.User.Email
            donor.FirstName = row.FR_Cart.User.FirstName
            donor.LastName = row.FR_Cart.User.LastName
            donor.MiddleName = getProfileProperty(row.FR_Cart.User, "MiddleName")
            donor.MobilePhone = getProfileProperty(row.FR_Cart.User, "Cell")
            donor.Phone = getProfileProperty(row.FR_Cart.User, "Telephone")
            donor.State = getProfileProperty(row.FR_Cart.User, "Region")
            donor.StreetAddress = getProfileProperty(row.FR_Cart.User, "Unit") & ", " & getProfileProperty(row.FR_Cart.User, "Street")
            donor.Zip = getProfileProperty(row.FR_Cart.User, "PostalCode")
            donor.UniqueDonorRef = row.FR_Cart.User.UserID

            insert.Donor = donor
            insert.GiftDate = row.FR_Cart.Date
            insert.UniqueDonationRef = "CC" & row.FR_Donations.First.DonationID

            donations.Add(insert)
        Next

        'Now add all of the virement
        Dim v = From c In d.Agape_Give_BankTransfers Where c.GiveMethod = 1 And c.Status = 0


        For Each row In v

            Dim insert As New FCX_API.Donation
            insert.Amount = row.Amount
            insert.PaymentType = "Repeat"


            If row.DonationType = DestinationType.Staff Then
                'staff
                Dim staff = From c In d.AP_StaffBroker_Staffs Where c.StaffId = row.TypeId

                If staff.Count > 0 Then
                    insert.DesigId = staff.First.AP_StaffBroker_StaffProfiles.Where(Function(x) x.AP_StaffBroker_StaffPropertyDefinition.FixedFieldName = "Designation(s)").Select(Function(x) x.PropertyValue).First
                End If
            Else
                'Departemnt or Project
                Dim dept = From c In d.AP_StaffBroker_Departments Where c.CostCenterId = row.TypeId

                If dept.Count > 0 Then
                    insert.DesigId = dept.First.PayType
                End If


            End If

            Dim donor As New FCX_API.Donor
            donor.City = getProfileProperty(row.User, "City")
            donor.Country = getProfileProperty(row.User, "Country")
            donor.Email = row.User.Email
            donor.FirstName = row.User.FirstName
            donor.LastName = row.User.LastName
            donor.MiddleName = getProfileProperty(row.User, "MiddleName")
            donor.MobilePhone = getProfileProperty(row.User, "Cell")
            donor.Phone = getProfileProperty(row.User, "Telephone")
            donor.State = getProfileProperty(row.User, "Region")
            donor.StreetAddress = getProfileProperty(row.User, "Unit") & ", " & getProfileProperty(row.User, "Street")
            donor.Zip = getProfileProperty(row.User, "PostalCode")
            donor.UniqueDonorRef = row.User.UserID

            insert.Donor = donor
            insert.GiftDate = row.SetupDate


            insert.UniqueDonationRef = "V" & row.VirId
            insert.IBAN = row.acNo
            insert.VCode = row.Reference

            donations.Add(insert)

        Next

        If donations.Count > 0 Then

            Dim resp = api.AddDonationBatch(api_key, UniqueRef, donations.ToArray)
            Console.Write(resp.Status)
            Console.Write(resp.Message)

            Console.ReadLine()
            If resp.Status = "SUCCESS" Then
                'Mark all lines as downloading
                For Each row In q
                    row.FR_Donations.First.BatchID = NewBatchRef
                    row.FR_Cart.OrderState = OrderState.Downloading
                Next
                For Each row In v
                    row.Status = OrderState.Downloading

                Next
                d.SubmitChanges()
            End If
        Else
            Console.Write("nothing to do")

        End If

    End Sub
    Private Function getProfileProperty(ByVal theUser As User, ByVal PropertyName As String) As String
        Return theUser.UserProfiles.Where(Function(x) x.ProfilePropertyDefinition.PropertyName = PropertyName).Select(Function(x) x.PropertyValue).First

    End Function


    Sub CreateTestBatch()

        Dim donor As New FCX_API.Donor
        donor.FirstName = "Jon"
        donor.LastName = "Vellacott"
        donor.Phone = "0121 707 5016"
        donor.UniqueDonorRef = "D00001"


        Dim donation As New FCX_API.Donation
        donation.Donor = donor
        donation.Amount = 100.0
        donation.UniqueDonationRef = "G00001"
        donation.PaymentType = "Credit Card"
        donation.GiftDate = Today
        Dim donations As New List(Of FCX_API.Donation)

        donations.Add(donation)


        api.AddDonationBatch(New System.Guid("401fe9e6-00c5-4198-ad59-0dd30c64304e"), "TEST001", donations.ToArray)





    End Sub

End Module
