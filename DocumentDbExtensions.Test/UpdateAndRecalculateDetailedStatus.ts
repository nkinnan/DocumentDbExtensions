// Note: All valid JavaScript is valid TypeScript (but not all valid TypeScript is valid JavaScript).
//       We are not using any of TypeScript's features, but by naming the file .ts, we do get at least
//       *some* error-checking (including some errors which are superfluous) that the visual studio .js 
//       editor doesn't provide.

function (ScenarioStatusByIdInput, FlightStatusByIdsInput) {
    var context = getContext();
    var response = context.getResponse();
    var collection = context.getCollection();

    // KEEP IN SYNC with enum DocumentType
    var documentTypeInvalid = 0;
    var documentTypeScenario = 1;
    var documentTypeScenarioHistory = 2;
    var documentTypeFlight = 3;
    var documentTypeFlightHistory = 4;

    // KEEP IN SYNC with DetailedStatusEnums.cs
    var flightStatusCategoryName = "Global";
    var flightStatusSnapshotSeriesName = "FlightStatus";
    var reportUploadAvailableCategoryName = "ReportUpload";
    var reportUploadAvailableWindowSeriesName = "Available";

    // KEEP IN SYNC with enum ScenarioStatus
    var publishedStateId = 3;

    var globalTimestamp = new Date().toISOString();

    // maintain results
    var result = {
        Errors: [],

        UpdatedScenarios: 0,
        SkippedScenarios: 0,

        UpdatedFlights: 0,
        SkippedFlights: 0,

        AddedCategories: 0,
        UpdatedCategories: 0,
        SkippedCategories: 0,

        AddedSnapshotSeries: 0,
        UpdatedSnapshotSeries: 0,
        SkippedSnapshotSeries: 0,

        AddedSnapshots: 0,
        DuplicateSnapshots: 0,
        SkippedSnapshots: 0,

        AddedWindowSeries: 0,
        UpdatedWindowSeries: 0,
        SkippedWindowSeries: 0,

        AddedWindows: 0,
        DuplicateWindows: 0,
        SkippedWindows: 0,

        SkippedScenarioStatusUpdates: 0,
        CompletedScenarioStatusUpdates: 0,

        UpdatedReportAvailableWindows: 0,
    };

    var IsValidDate = function (d) {
        if (Object.prototype.toString.call(d) === "[object Date]") {
            // it is a date
            if (isNaN(d.getTime())) {  // d.valueOf() could also work
                // date is not valid
                return false;
            }
            else {
                // date is valid
                return true;
            }
        }
        else {
            // not a date
            return false;
        }
    }

    var SortByTimestamp = function (a, b) {
        if (a.Timestamp < b.Timestamp) return -1;
        if (a.Timestamp > b.Timestamp) return 1;
        return 0;
    }

    var SortByEnd = function (a, b) {
        if (a.End < b.End) return -1;
        if (a.End > b.End) return 1;
        return 0;
    }

    var temp_fix_cannonicalizeStatusUpdateValue = function (scenarioStatusString) {

        if (scenarioStatusString != '' && isNaN(scenarioStatusString)) {
            if (scenarioStatusString == 'InvalidStatus' ||
                scenarioStatusString == 'Submitted' ||
                scenarioStatusString == 'Failed' ||
                scenarioStatusString == 'Published' ||
                scenarioStatusString == 'Expired' ||
                scenarioStatusString == 'Deactivated' ||
                scenarioStatusString == 'Approved' ||
                scenarioStatusString == 'Rejected' ||
                scenarioStatusString == 'Completed') {
                return scenarioStatusString;
            }
            else {
                throw new Error("scenarioStatusString is a non-numeric but not a recognized value");
            }
        }
        else {
            var asInt = statusStringToInt(scenarioStatusString);

            if (scenarioStatusString == 0) return 'InvalidStatus';
            if (scenarioStatusString == 1) return 'Submitted';
            if (scenarioStatusString == 2) return 'Failed';
            if (scenarioStatusString == 3) return 'Published';
            if (scenarioStatusString == 4) return 'Expired';
            if (scenarioStatusString == 5) return 'Deactivated';
            if (scenarioStatusString == 6) return 'Approved';
            if (scenarioStatusString == 7) return 'Rejected';
            if (scenarioStatusString == 8) return 'Completed';
        }

        throw new Error('Unknown (2) scenario status string: ' + scenarioStatusString);
    }

    var statusStringToInt = function (scenarioStatusString) {
        // this can be removed once the bugfix RIs
        if (scenarioStatusString != '' && !isNaN(scenarioStatusString)) {
            var asNumber = +scenarioStatusString;
            if (!(asNumber >= 0 && asNumber <= 8)) {
                throw new Error("scenarioStatusString is a number but not within the allowed range");
            }
            return asNumber;
        }

        if (scenarioStatusString == 'InvalidStatus') return 0;
        if (scenarioStatusString == 'Submitted') return 1;
        if (scenarioStatusString == 'Failed') return 2;
        if (scenarioStatusString == 'Published') return 3;
        if (scenarioStatusString == 'Expired') return 4;
        if (scenarioStatusString == 'Deactivated') return 5;
        if (scenarioStatusString == 'Approved') return 6;
        if (scenarioStatusString == 'Rejected') return 7;
        if (scenarioStatusString == 'Completed') return 8;

        throw new Error('Unknown scenario status string: ' + scenarioStatusString);
    }

    // helper method to copy and recalculate the status
    var CopyStatusAndMaintainLatestAndAggregates = function (id, sourceStatusCategories, targetStatusCategories) {
        sourceStatusCategories.forEach(function (sourceStatusCategory) {

            if (!sourceStatusCategory.Name) {
                result.Errors.push('Invalid category name: ' + sourceStatusCategory.Name + ' id: ' + id);
                result.SkippedCategories++;
                return;
            }

            // get or add the named status category
            var targetStatusCategory = targetStatusCategories.filter(function (item) { return item.Name == sourceStatusCategory.Name; });
            if (!targetStatusCategory || targetStatusCategory.length == 0) {
                targetStatusCategory = { Name: sourceStatusCategory.Name, SnapshotStatuses: [], WindowStatuses: [] };
                targetStatusCategories.push(targetStatusCategory);
                result.AddedCategories++;
            }
            else {
                targetStatusCategory = targetStatusCategory[0];
                result.UpdatedCategories++;
            }

            if (sourceStatusCategory.SnapshotStatuses && sourceStatusCategory.SnapshotStatuses.length != 0) {
                // add/update the snapshot series while maintaining latest value
                sourceStatusCategory.SnapshotStatuses.forEach(function (sourceSnapshotSeries) {

                    if (!sourceSnapshotSeries.Name) {
                        result.Errors.push('Invalid snapshot series name: ' + sourceSnapshotSeries.Name + ' id: ' + id);
                        result.SkippedSnapshotSeries++;
                        return;
                    }

                    if (!sourceSnapshotSeries.History || sourceSnapshotSeries.History.length == 0) {
                        result.Errors.push('Empty snapshot series: ' + sourceSnapshotSeries.Name + ' id: ' + id);
                        result.SkippedSnapshotSeries++;
                        return;
                    }

                    // get or add the snapshot series
                    var targetSnapshotSeries = targetStatusCategory.SnapshotStatuses.filter(function (item) { return item.Name == sourceSnapshotSeries.Name; });
                    if (!targetSnapshotSeries || targetSnapshotSeries.length == 0) {
                        targetSnapshotSeries = { Name: sourceSnapshotSeries.Name, History: [] };
                        targetStatusCategory.SnapshotStatuses.push(targetSnapshotSeries);
                        result.AddedSnapshotSeries++;
                    }
                    else {
                        targetSnapshotSeries = targetSnapshotSeries[0];
                        result.UpdatedSnapshotSeries++;
                    }

                    // add the history list if not present
                    if (!targetSnapshotSeries.History)
                        targetSnapshotSeries.History = [];

                    // copy the new snapshots
                    sourceSnapshotSeries.History.forEach(function (sourceSnapshot) {
                        if (!sourceSnapshot.Message)
                            sourceSnapshot.Message = null;

                        // temp fix
                        if (targetSnapshotSeries.Name == 'FlightStatus') {
                            sourceSnapshot.Value = temp_fix_cannonicalizeStatusUpdateValue(sourceSnapshot.Value);
                        }

                        // check validity and standardise date
                        var ts = new Date(sourceSnapshot.Timestamp);
                        if (!IsValidDate(ts)) {
                            result.Errors.push('Bad snapshot Timestamp property: ' + sourceSnapshot.Timestamp + ' id: ' + id);
                            result.SkippedSnapshots++;
                            return;
                        }
                        sourceSnapshot.Timestamp = ts.toISOString();

                        // don't double-add (idempotency)
                        var dupe = targetSnapshotSeries.History.filter(function (item) {

                            // if a timestamp has zero milliseconds then the fractional part will be left out on DocDB serialization even though we put it there
                            // so we have to re-parse anything retrieved from the DB to ensure equality comparisons pass, this is a JSON serialization issue
                            var itemTimestamp = new Date(item.Timestamp).toISOString();

                            var hit = (itemTimestamp == sourceSnapshot.Timestamp);
                            return hit;
                        });
                        if (dupe && dupe.length != 0) {
                            // check if attempting to update value
                            if (dupe[0].Value != sourceSnapshot.Value) {
                                result.Errors.push('Attempt to update snapshot Value with same Timestamp: ' + sourceSnapshot.Timestamp + ' ("' + dupe[0].Value + '" -> "' + sourceSnapshot.Value + '")' + ' id: ' + id);
                                result.SkippedSnapshots++;
                                return;
                            }
                            // simply a duplicate
                            result.DuplicateSnapshots++;
                            return;
                        }

                        // note: we are NOT de-duplicating if "status hasn't changed" because we can't guarantee we're passed a properly sorted list
                        //       and even if we sorted it outself, we could be called with out of order lists - this could cause valid edges to be dropped

                        // maintain latest values
                        if (!targetSnapshotSeries.LatestTimestamp || sourceSnapshot.Timestamp > targetSnapshotSeries.LatestTimestamp) {
                            targetSnapshotSeries.LatestTimestamp = sourceSnapshot.Timestamp;
                            targetSnapshotSeries.LatestValue = sourceSnapshot.Value;
                            targetSnapshotSeries.LatestMessage = sourceSnapshot.Message;
                        }

                        targetSnapshotSeries.History.push(sourceSnapshot);
                        result.AddedSnapshots++;
                    });

                    // we don't *really* need to do this...
                    targetSnapshotSeries.History.sort(SortByTimestamp);
                });
            }

            if (sourceStatusCategory.WindowStatuses && sourceStatusCategory.WindowStatuses.length != 0) {
                // add/update the window series while maintaining latest value
                sourceStatusCategory.WindowStatuses.forEach(function (sourceWindowSeries) {
                    if (!sourceWindowSeries.Message)
                        sourceWindowSeries.Message = null;

                    if (!sourceWindowSeries.Name) {
                        result.Errors.push('Invalid window series name: ' + sourceWindowSeries.Name + ' id: ' + id);
                        result.SkippedWindowSeries++;
                        return;
                    }

                    if (!sourceWindowSeries.History || sourceWindowSeries.History.length == 0) {
                        result.Errors.push('Empty window series: ' + sourceWindowSeries.Name + ' id: ' + id);
                        result.SkippedWindowSeries++;
                        return;
                    }

                    // get or add the window series
                    var targetWindowSeries = targetStatusCategory.WindowStatuses.filter(function (item) { return item.Name == sourceWindowSeries.Name; });
                    if (!targetWindowSeries || targetWindowSeries.length == 0) {
                        targetWindowSeries = { Name: sourceWindowSeries.Name, TotalValue: 0, History: [] };
                        targetStatusCategory.WindowStatuses.push(targetWindowSeries);
                        result.AddedWindowSeries++;
                    }
                    else {
                        targetWindowSeries = targetWindowSeries[0];
                        result.UpdatedWindowSeries++;
                    }

                    // add the history list if not present
                    if (!targetWindowSeries.History)
                        targetWindowSeries.History = [];

                    // copy the new windows
                    sourceWindowSeries.History.forEach(function (sourceWindow) {
                        if (!sourceWindow.Message)
                            sourceWindow.Message = null;

                        // check validity and standardise date
                        var start = new Date(sourceWindow.Start);
                        if (!IsValidDate(start)) {
                            result.Errors.push('Bad window Start property: ' + sourceWindow.Start + ' id: ' + id);
                            result.SkippedWindows++;
                            return;
                        }
                        sourceWindow.Start = start.toISOString();
                        var end = new Date(sourceWindow.End);
                        if (!IsValidDate(end)) {
                            result.Errors.push('Bad window End property: ' + sourceWindow.End + ' id: ' + id);
                            result.SkippedWindows++;
                            return;
                        }
                        sourceWindow.End = end.toISOString();

                        // check Start not >= End
                        if (sourceWindow.Start >= sourceWindow.End) {
                            result.Errors.push('Attempt to write window with Start >= End, Start: ' + sourceWindow.Start + ' End: ' + sourceWindow.End + ' id: ' + id);
                            result.SkippedWindows++;
                            return;
                        }

                        // don't double-add (idempotency)
                        var dupe = targetWindowSeries.History.filter(function (item) {

                            // if a timestamp has zero milliseconds then the fractional part will be left out on DocDB serialization even though we put it there
                            // so we have to re-parse anything retrieved from the DB to ensure equality comparisons pass, this is a JSON serialization issue
                            var itemStart = new Date(item.Start).toISOString();
                            var itemEnd = new Date(item.End).toISOString();

                            var hit = (itemStart == sourceWindow.Start && itemEnd == sourceWindow.End);
                            return hit;
                        });
                        var existingReportAvailableWindow = null;
                        if (dupe && dupe.length != 0) {
                            // SPECIAL CASE: ReportAvailable is the only situation where we actually expect "update" since its real-time and not actually an aggregate
                            if (sourceStatusCategory.Name == reportUploadAvailableCategoryName &&
                                sourceWindowSeries.Name == reportUploadAvailableWindowSeriesName) {
                                existingReportAvailableWindow = dupe[0];
                            } else {
                                // check if attempting to update value
                                if (dupe[0].Value != sourceWindow.Value) {
                                    result.Errors.push('Attempt to update window Value with same time range, Start: ' + sourceWindow.Start + ' End: ' + sourceWindow.End + ' ("' + dupe[0].Value + '" -> "' + sourceWindow.Value + '")' + ' id: ' + id);
                                    result.SkippedWindows++;
                                    return;
                                }
                                // simply a duplicate
                                result.DuplicateWindows++;
                                return;
                            }
                        }

                        // SPECIAL CASE: ReportAvailable is the only situation where we actually expect "update" since its real-time and not actually an aggregate
                        if (!existingReportAvailableWindow) {
                            // don't allow window overlap
                            var overlap = targetWindowSeries.History.filter(function (item) {

                                // if a timestamp has zero milliseconds then the fractional part will be left out on DocDB serialization even though we put it there
                                // so we have to re-parse anything retrieved from the DB to ensure equality comparisons pass, this is a JSON serialization issue
                                var itemStart = new Date(item.Start).toISOString();
                                var itemEnd = new Date(item.End).toISOString();

                                var hit =
                                    // overlaps start
                                    ((itemStart < sourceWindow.Start && itemEnd > sourceWindow.Start) ||
                                    // overlaps end
                                    (itemEnd > sourceWindow.End && itemStart < sourceWindow.End) ||
                                    // entirely contained (a in b)
                                    (itemStart <= sourceWindow.Start && itemEnd >= sourceWindow.End) ||
                                    // entirely contained (b in a)
                                    (sourceWindow.Start <= itemStart && sourceWindow.End >= itemEnd));
                                return hit;
                            });
                            if (overlap && overlap.length != 0) {
                                result.Errors.push('Attempt to write overlapping window time range, Start: ' + sourceWindow.Start + ' End: ' + sourceWindow.End + ' (Start: ' + overlap[0].Start + ' End: ' + overlap[0].End + ')' + ' sourceValue: ' + sourceWindow.Value + ' targetValue: ' + overlap[0].Value + ' dupe: ' + JSON.stringify(dupe) + ' id: ' + id);
                                result.SkippedWindows++;
                                return;
                            }
                        }

                        // maintain latest values
                        if (!targetWindowSeries.LatestEnd || sourceWindow.End > targetWindowSeries.LatestEnd) {
                            targetWindowSeries.LatestStart = sourceWindow.Start;
                            targetWindowSeries.LatestEnd = sourceWindow.End;
                            targetWindowSeries.LatestMessage = sourceWindow.Message;
                        }

                        // always aggregate the total for windows
                        targetWindowSeries.TotalValue += sourceWindow.Value;

                        // SPECIAL CASE: ReportAvailable is the only situation where we actually expect "update" since its real-time and not actually an aggregate
                        if (sourceStatusCategory.Name == reportUploadAvailableCategoryName &&
                            sourceWindowSeries.Name == reportUploadAvailableWindowSeriesName &&
                            existingReportAvailableWindow != null) {
                            existingReportAvailableWindow.Value += sourceWindow.Value;
                            result.UpdatedReportAvailableWindows++;
                        }
                        else {
                            targetWindowSeries.History.push(sourceWindow);
                            result.AddedWindows++;
                        }
                    });

                    // we don't *really* need to do this...
                    targetWindowSeries.History.sort(SortByEnd);
                });
            }
        });
    }

    // helper method to query the DB by Scenarios.ControlTowerFlightId
    var getDocByControlTowerFlightIdAsync = function (controlTowerFlightId, callback) {
        var filterQuery = 'SELECT * FROM Scenarios where Scenarios.ControlTowerFlightId = "' + controlTowerFlightId + '" and Scenarios.DocumentType = ' + documentTypeFlight;
        var accepted = collection.queryDocuments(
            collection.getSelfLink(),
            filterQuery,
            {},
            function (error, resources, options) {
                var existing;

                if (error) {
                    // we're OK if the document doesn't exist, but DocumentDB treats this as an error
                    // note: docdb is using a reserved keyword as a property name thus the special syntax
                    if (error["number"] != 404)
                        throw new Error('Error querying existing document: ' + error.message);
                }

                if (resources && Array.isArray(resources) && resources.length != 0)
                    existing = resources[0];

                callback(existing);
            });
        if (!accepted)
            throw new Error('__RESOURCE_USAGE_YIELD__: collection.queryDocuments() returned false due to rate-limiting.');
    }

    // helper method to get a document by Scenarios.Id (PK)
    var getDocByIdAsync = function (id, callback) {
        var accepted = collection.readDocument(
            collection.getAltLink() + '/docs/' + id,
            {},
            function (error, resource, options) {
                var existing;

                if (error) {
                    // we're OK if the document doesn't exist, but DocumentDB treats this as an error
                    // note: docdb is using a reserved keyword as a property name thus the special syntax
                    if (error["number"] != 404)
                        throw new Error('Error querying existing document: ' + error.message);
                }

                if (resource)
                    existing = resource;

                callback(existing);
            });
        if (!accepted)
            throw new Error('__RESOURCE_USAGE_YIELD__: collection.readDocument() returned false due to rate-limiting');
    }

    // helper method to replace a document
    var replaceDocAsync = function (existing, callback) {
        var accepted = collection.replaceDocument
            (
            existing._self,
            existing,
            {},
            function (error, resource, options) {
                if (error) {
                    // snapshot isolation mode means we see a consistent DB state, but if someone modifies a document 
                    // external to the sproc then we are no longer able to write it ourselves (and because of the snapshot
                    // we can't attempt to read the latest version and update that either - sproc must simply rollback)
                    if (error["number"] == 449)
                        throw new Error('Document (' + existing._self + ') was modified externally during sproc execution (snapshot violation): ' + error.message);

                    throw new Error('Unable to update document: ' + error.message);
                }

                callback();
            });
        if (!accepted)
            throw new Error('__RESOURCE_USAGE_YIELD__: collection.replaceDocument() returned false due to rate-limiting.');
    }

    var tryGetLatestSnapshotValue = function (categoryList, categoryName, snapshotSeriesName) {
        if (categoryList && categoryList.length != 0) {
            var category = categoryList.filter(function (item) { return item.Name == categoryName; });
            if (!category || category.length == 0)
                return null;
            else
                category = category[0];

            if (category.SnapshotStatuses && category.SnapshotStatuses.length != 0) {
                var snapshotSeries = category.SnapshotStatuses.filter(function (item) { return item.Name == snapshotSeriesName; });
                if (!snapshotSeries || snapshotSeries.length == 0)
                    return null;
                else
                    snapshotSeries = snapshotSeries[0];

                return { LatestValue: snapshotSeries.LatestValue, LatestMessage: snapshotSeries.LatestMessage };
            }
        }
    }

    var tryGetLatestWindowValue = function (categoryList, categoryName, windowSeriesName) {
        if (categoryList && categoryList.length != 0) {
            var category = categoryList.filter(function (item) { return item.Name == categoryName; });
            if (!category || category.length == 0)
                return null;
            else
                category = category[0];

            if (category.WindowStatuses && category.WindowStatuses.length != 0) {
                var windowSeries = category.WindowStatuses.filter(function (item) { return item.Name == windowSeriesName; });
                if (!windowSeries || windowSeries.length == 0)
                    return null;
                else
                    windowSeries = windowSeries[0];

                return { TotalValue: windowSeries.TotalValue, LatestMessage: windowSeries.LatestMessage };
            }
        }
    }

    var alsoUpdateChildScenariosFlightStatusAsync = function (scenarioIds, newStatus, newMessage, callback) {

        // early exit test
        if (!scenarioIds || scenarioIds.length == 0) {
            // nothing to iterate on, execute the callback
            callback();
            return;
        }

        var loop = function (index) {

            // loop completion test
            if (index == scenarioIds.length) {
                // finished iterating the array, execute the callback
                callback();
                return;
            }

            var scenarioId = scenarioIds[index];

            getDocByIdAsync(scenarioId, function (scenarioDoc) {

                if (!scenarioDoc) {
                    result.Errors.push('Unable to look up scenario document with Id: ' + scenarioId);
                    result.SkippedScenarioStatusUpdates++;
                    // execute against next scenarioId
                    loop(index + 1);
                    return;
                }

                if (scenarioDoc.DocumentType != documentTypeScenario) {
                    result.Errors.push('Scenario document with Id: ' + scenarioId + ' has DocumentType: ' + scenarioDoc.DocumentType);
                    result.SkippedScenarioStatusUpdates++;
                    // execute against next scenarioId
                    loop(index + 1);
                    return;
                }

                // this was in the SQL DB sprocs but its causing issues now and we don't really care
                ////if (scenarioDoc.PartnerId != 1) {
                ////    result.Errors.push('Scenario document with Id: ' + scenarioId + ': You can only update a Windows ScenarioStatus!');
                ////    result.SkippedScenarioStatusUpdates++;
                ////    // execute against next scenarioId
                ////    loop(index + 1);
                ////    return;
                ////}

                // convert enum name to int value
                newStatus = statusStringToInt(newStatus);

                // set published date if necessary
                if (scenarioDoc.ScenarioStatusId != publishedStateId &&
                    newStatus == publishedStateId) {
                    scenarioDoc.ScenarioDeployDate = globalTimestamp;
                }

                scenarioDoc.ScenarioStatusId = newStatus;
                scenarioDoc.Message = newMessage;

                // even though ScenarioStatusId shouldn't even be on Scenario, we need to update LastUpdated for the 
                // same reason we maintain the deprecated status property in the first place: backwards compatability
                scenarioDoc.UpdateDate = globalTimestamp;
                scenarioDoc.ScenarioUpdateDate = globalTimestamp; // this property itself is also legacy copy (legacy inception?)

                replaceDocAsync(scenarioDoc, function () {
                    result.CompletedScenarioStatusUpdates++;

                    // execute against next scenarioId
                    loop(index + 1);
                    return;
                });
            });
        }

        // start the loop
        loop(0);
    }

    // method to update all the Scenario statuses
    var updateScenarioDetailedStatusAsync = function (NewScenarioStatusById, callback) {

        // early exit test
        if (!NewScenarioStatusById || NewScenarioStatusById.length == 0) {
            // nothing to iterate on, execute the callback
            callback();
            return;
        }

        var loop = function (index) {

            // loop completion test
            if (index == NewScenarioStatusById.length) {
                // finished iterating the array, execute the callback
                callback();
                return;
            }

            var kvp = NewScenarioStatusById[index];

            var docId = kvp.Key;

            if (!docId) {
                result.Errors.push('Invalid scenario Id: ' + docId);
                result.SkippedScenarios++;
                // execute against next scenario status
                loop(index + 1);
                return;
            }

            var sourceStatusCategories = kvp.Value;

            if (!sourceStatusCategories || sourceStatusCategories.length == 0) {
                result.Errors.push('No categories to update for scenario document with Id: ' + docId);
                result.SkippedScenarios++;
                // execute against next scenario status
                loop(index + 1);
                return;
            }

            getDocByIdAsync(docId, function (scenarioDoc) {
                if (!scenarioDoc) {
                    result.Errors.push('Unable to look up scenario document with Id: ' + docId);
                    result.SkippedScenarios++;
                    // execute against next scenario status
                    loop(index + 1);
                    return;
                }

                if (scenarioDoc.DocumentType != documentTypeScenario) {
                    result.Errors.push('Scenario document with Id: ' + docId + ' has DocumentType: ' + scenarioDoc.DocumentType);
                    result.SkippedScenarios++;
                    // execute against next scenario status
                    loop(index + 1);
                    return;
                }

                // this was in the SQL DB sprocs but its causing issues now and we don't really care
                ////if (scenarioDoc.PartnerId != 1) {
                ////    result.Errors.push('Scenario document with Id: ' + docId + ': You can only update a Windows ScenarioStatus!');
                ////    result.SkippedScenarios++;
                ////    // execute against next scenario status
                ////    loop(index + 1);
                ////    return;
                ////}

                if (!scenarioDoc.DetailedStatus)
                    scenarioDoc.DetailedStatus = [];

                var targetStatusCategories = scenarioDoc.DetailedStatus;

                var reportsAvailableBefore = tryGetLatestWindowValue(targetStatusCategories, reportUploadAvailableCategoryName, reportUploadAvailableWindowSeriesName);
                CopyStatusAndMaintainLatestAndAggregates(docId, sourceStatusCategories, targetStatusCategories);
                var reportsAvailableAfter = tryGetLatestWindowValue(targetStatusCategories, reportUploadAvailableCategoryName, reportUploadAvailableWindowSeriesName);

                // UGLY HACK BUG UGLY
                // todo: remove this once we wean our partners off of this property and onto the DetailedStatus alternative
                // if the specific window series related to ReportsAvailable has been updated, then also copy TotalValue over into 
                // the CabCountReceived property
                if ((reportsAvailableBefore == null && reportsAvailableAfter != null) ||
                    (reportsAvailableBefore && reportsAvailableAfter && reportsAvailableBefore.TotalValue != reportsAvailableAfter.TotalValue))
                    scenarioDoc.CabCountReceived = reportsAvailableAfter.TotalValue;

                scenarioDoc.DetailedStatusLastUpdated = globalTimestamp;

                replaceDocAsync(scenarioDoc, function () {
                    result.UpdatedScenarios++;

                    // execute against next scenario status
                    loop(index + 1);
                    return;
                });
            });
        }

        // start the loop
        loop(0);
    }

    // method to update all the Flight statuses
    var updateFlightDetailedStatusAsync = function (NewFlightStatusByIds, callback) {

        // early exit test
        if (!NewFlightStatusByIds || NewFlightStatusByIds.length == 0) {
            // nothing to iterate on, execute the callback
            callback();
            return;
        }

        var loop = function (index) {

            // loop completion test
            if (index == NewFlightStatusByIds.length) {
                // finished iterating the array, execute the callback
                callback();
                return;
            }

            var kvp = NewFlightStatusByIds[index];

            var docId = kvp.Key;
            var isControlTowerFlightId = !isNaN(docId);

            if (!docId) {
                result.Errors.push('Invalid flight Id: ' + docId);
                result.SkippedFlights++;
                // execute against next flight status
                loop(index + 1);
                return;
            }

            var sourceStatusCategories = kvp.Value;

            if (!sourceStatusCategories || sourceStatusCategories.length == 0) {
                if (isControlTowerFlightId)
                    result.Errors.push('No categories to update for flight document with ControlTowerFlightId: ' + docId);
                else
                    result.Errors.push('No categories to update for flight document with Id: ' + docId);
                result.SkippedFlights++;
                // execute against next flight status
                loop(index + 1);
                return;
            }

            // callback method to handle the update/replace once the Flight doc has been retrieved
            var updateFlightDoc = function (flightDoc) {
                if (!flightDoc) {
                    if (isControlTowerFlightId)
                        result.Errors.push('Unable to look up flight document with ControlTowerFlightId: ' + docId);
                    else
                        result.Errors.push('Unable to look up flight document with Id: ' + docId);
                    result.SkippedFlights++;
                    // execute against next flight status
                    loop(index + 1);
                    return;
                }

                if (flightDoc.DocumentType != documentTypeFlight) {
                    result.Errors.push('Flight document with Id: ' + docId + ' has DocumentType: ' + flightDoc.DocumentType);
                    result.SkippedFlights++;
                    // execute against next flight status
                    loop(index + 1);
                    return;
                }

                if (!flightDoc.DetailedStatus)
                    flightDoc.DetailedStatus = [];

                var targetStatusCategories = flightDoc.DetailedStatus;

                var flightStatusBefore = tryGetLatestSnapshotValue(targetStatusCategories, flightStatusCategoryName, flightStatusSnapshotSeriesName);
                CopyStatusAndMaintainLatestAndAggregates(docId, sourceStatusCategories, targetStatusCategories);
                var flightStatusAfter = tryGetLatestSnapshotValue(targetStatusCategories, flightStatusCategoryName, flightStatusSnapshotSeriesName);

                flightDoc.DetailedStatusLastUpdated = globalTimestamp;

                replaceDocAsync(flightDoc, function () {
                    result.UpdatedFlights++;

                    // UGLY HACK BUG UGLY
                    // todo: remove this once we wean our partners off this ridiculous storage location
                    // if the specific snapshot series related to flight status has been updated, then also copy status over into all 
                    // scenario docs pointed to by doc.ScenarioIds (set the ScenarioStatusId property)
                    if ((flightStatusBefore == null && flightStatusAfter != null) ||
                        (flightStatusBefore && flightStatusAfter && flightStatusBefore.LatestValue != flightStatusAfter.LatestValue)) {
                        alsoUpdateChildScenariosFlightStatusAsync(flightDoc.ScenarioIds, flightStatusAfter.LatestValue, flightStatusAfter.LatestMessage, function () {
                            // execute against next flight status
                            loop(index + 1);
                            return;
                        });
                    }

                    // execute against next flight status
                    loop(index + 1);
                    return;
                });
            }

            // get either by Id or ControlTowerFlightId and then execute the callback above to do the update/replace
            if (isControlTowerFlightId)
                getDocByControlTowerFlightIdAsync(docId, updateFlightDoc);
            else // PK
                getDocByIdAsync(docId, updateFlightDoc);
        }

        // start the loop
        loop(0);
    }

    // begin the async execution chain (this is the "entry point" to the sproc)
    updateScenarioDetailedStatusAsync(ScenarioStatusByIdInput, function () {
        updateFlightDetailedStatusAsync(FlightStatusByIdsInput, function () {
            response.setBody(result);
        });
    });
}
