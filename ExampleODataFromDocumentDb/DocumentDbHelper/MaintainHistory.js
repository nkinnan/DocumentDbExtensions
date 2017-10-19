// Note: All valid JavasScript is valid TypeScript (but not all valid TypeScript is valid JavasSript).
//       We are not using any of TypeScript's features, but by naming the file .ts, we do get at least
//       *some* error-checking (including some errors which are superfluous) that the visual studio .js 
//       editor doesn't provide.
function () {
    // grab context
    var context = getContext();
    var request = context.getRequest();
    var collection = context.getCollection();
    // KEEP IN SYNC with enum DocumentType
    var documentTypeInvalid = 0;
    var documentTypeHouse = 1;
    var documentTypeHouseHistory = 2;
    // get additional info we'll need, not sure how to get the user in DocumentDB, might not be possible
    var timestamp = new Date().toISOString();
    var pending = request.getBody();
    var existing;
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
    var createHistoryRecord = function (from, changeType) {
        // copy existing into a new object, omitting the private Document properties since we'll be creating a new document not updating an existing one
        var doc = duplicateDocument(from);
        // update record type
        if (doc.DocumentType == documentTypeHouse)
            doc.DocumentType = documentTypeHouseHistory;
        // add history record properties
        doc.OriginalId = from.id;
        doc.ModifyAction = changeType;
        doc.ModifyTimeStamp = timestamp;
        // queue the history record for creation
        accepted = collection.createDocument(collection.getSelfLink(), doc, { disableAutomaticIdGeneration: false }, function (error, resource, options) {
            if (error)
                throw new Error('Unable to create new historical record from existing record: ' + error.message);
        });
        if (!accepted)
            throw new Error('collection.createDocument() to create new historical record returned false due to rate-limiting.');
    };
    // we do this on every request regardless of type, it modifies the request in-flight before it hits the DB
    var modifyRequest = function () {
        // nothing that IsHistorical ever gets modified, so set that to false on any operation
        if (pending.IsHistorical != false)
            throw new Error("Attempt to update a historical record");
        // set create timestamp if applicable, else set last update timestamp
        if (existing) {
            pending.UpdateDate = timestamp;
        }
        else {
            pending.CreateDate = timestamp;
        }
        // update request on the fly
        request.setBody(pending);
    };
    // ------ This is where everything kicks off and the above functions are called.
    // get the existing document (with both exists and !exists paths inside the callback)
    var accepted = collection.readDocument(collection.getAltLink() + '/docs/' + pending.id, // can't use 'pending._self' because it might not be filled in, particularly on insert, but possibly on replace as well
    {}, function (error, resource, options) {
        if (error) {
            // we're OK if the document doesn't exist, but DocumentDB treats this as an error
            // note: docdb is using a reserved keyword as a property name thus the special syntax
            if (error["number"] != 404)
                throw new Error('Error querying existing document: ' + error.message);
        }
        if (resource)
            existing = resource;
        // ==== NOTE ==== You *could* enumerate and compare properties and have a more sensible history record that simply lists the properties that have changed with old and new values (you could even do this recursively) 
        // ---- before update, record old version
        if (existing) {
            createHistoryRecord(existing, "Update");
        }
        else {
            createHistoryRecord(pending, "Create");
        }
        // finally, modify the request in-flight to keep timestamps up to date (and anything else you may want to do "standard")
        modifyRequest();
    });
    if (!accepted)
        throw new Error('collection.readDocument() to get existing record returned false due to rate-limiting');
}
