// Note: All valid JavasScript is valid TypeScript (but not all valid TypeScript is valid JavasSript).
//       We are not using any of TypeScript's features, but by naming the file .ts, we do get at least
//       *some* error-checking (including some errors which are superfluous) that the visual studio .js 
//       editor doesn't provide.
function () {
    // grab context and other bits of info we'll need
    var context = getContext();
    var request = context.getRequest();
    var pending = request.getBody();
    var operation = request.getOperationType();
    var collection = context.getCollection();
    var timestamp = new Date().toISOString();
    // DocumentDB constants
    var operationType = {
        create: "Create",
        upsert: "Upsert",
        replace: "Replace",
        delete: "Delete",
    };
    // IMPORTANT: keep in sync with the C# enum DocumentType
    var documentType = {
        invalid: 0,
        house: 1,
        houseHistory: 2,
    };
    var isHistoryRecord = pending && pending.DocumentType == documentType.houseHistory;
    // ------ because of how JavaScript works we're going to set up a bunch of nested functions as variables
    // ------ this enables us to write much more readable code instead of nesting everything inside callbacks N levels deep
    // ------ and also to avoid inlining code that logically should be seperated
    // helper method to test if an array contains an object
    var arrayContains = function (a, obj) {
        for (var i = 0; i < a.length; i++) {
            if (a[i] === obj)
                return true;
        }
        return false;
    };
    // property name array that we'll use below, internal DocumentDB properties that we don't want to copy when duplicating a document
    // NOTE: this DOES include '_attachments' meaning the attachment references will not be retained (it would be a pain to copy them and update the references in this trigger, and we're not using them anyway, but it is possible...)
    var ignoredProperties = ['id', '_rid', '_self', '_ts', '_etag', '_attachments'];
    // helper method to duplicate a document
    var duplicateDocument = function (obj) {
        var duplicated = {};
        Object.keys(obj).forEach(function (key) {
            if (!arrayContains(ignoredProperties, key))
                duplicated[key] = obj[key];
        });
        return duplicated;
    };
    // create a history record recording this change
    // ==== NOTE ==== You *could* enumerate and compare properties and have a more sensible history record that simply lists the properties that have changed with old and new values (you could even do this recursively) 
    var createHistoryRecord = function (from, changeType) {
        // copy existing into a new object, omitting the private Document properties since we'll be creating a new document not updating an existing one
        var doc = duplicateDocument(from);
        // update record type
        if (doc.DocumentType == documentType.house)
            doc.DocumentType = documentType.houseHistory;
        // add history record properties
        doc.OriginalId = from.id;
        doc.ModifyAction = changeType;
        doc.ModifyTimeStamp = timestamp;
        // queue the history record for creation
        accepted = collection.createDocument(collection.getSelfLink(), doc, { disableAutomaticIdGeneration: false }, function (error, resource, options) {
            if (error)
                throw new Error('Unable to create new history record from existing document: ' + error.message);
        });
        if (!accepted)
            throw new Error('collection.createDocument() to create new history record returned false due to rate-limiting.');
    };
    // we do this on every request regardless of type, it modifies the request in-flight before it hits the DB
    var maintainTimestamps = function () {
        // set create timestamp if applicable, else set last update timestamp
        if (operation == operationType.create) {
            pending.CreateDate = timestamp;
        }
        else {
            pending.UpdateDate = timestamp;
        }
        // update request on the fly
        request.setBody(pending);
    };
    // ------ This is where everything kicks off and the above functions are called.
    // nothing that IsHistorical ever gets modified, so set that to false on any operation
    if (isHistoryRecord)
        throw new Error("Attempt to update a historical record");
    // modify the request in-flight to keep timestamps up to date (and anything else you may want to do "standard")
    maintainTimestamps();
    // we need different paths depending on operation type and whether an existing document is present in the DB
    if (operation == operationType.create) {
        // there is no existing document, record the pending insert
        createHistoryRecord(pending, operation);
    }
    else {
        // get the existing document
        var accepted = collection.readDocument(collection.getAltLink() + '/docs/' + pending.id, // can't use 'pending._self' because it might not be filled in, particularly on insert
        {}, function (error, existing, options) {
            if (error) {
                // we're OK if the document doesn't exist, but DocumentDB treats this as an error
                // note: docdb is using a reserved keyword as a property name thus the special syntax
                if (error["number"] != 404)
                    throw new Error('Error querying existing document: ' + error.message);
            }
            // if we hit this path, the existing document should always be available
            if (!existing)
                throw new Error('Existing document not available on ' + operation + ' operation');
            // maintain history records
            // its important that this call is inside the readDocument() callback and that the callbacks are chained in order to prevent trigger blacklisting
            createHistoryRecord(existing, operation);
        });
        if (!accepted)
            throw new Error('collection.readDocument() to get existing record returned false due to rate-limiting');
    }
}
