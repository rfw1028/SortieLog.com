{
    "$schema": "http://json-schema.org/draft-04/schema#",
        "type": "array",
            "items": [
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string"
                        },
                        "icaoId": {
                            "type": "string"
                        },
                        "iataId": {
                            "type": "string"
                        },
                        "faaId": {
                            "type": "string"
                        },
                        "name": {
                            "type": "string"
                        },
                        "state": {
                            "type": "string"
                        },
                        "country": {
                            "type": "string"
                        },
                        "source": {
                            "type": "string"
                        },
                        "type": {
                            "type": "string"
                        },
                        "lat": {
                            "type": "number"
                        },
                        "lon": {
                            "type": "number"
                        },
                        "elev": {
                            "type": "integer"
                        },
                        "magdec": {
                            "type": "string"
                        },
                        "owner": {
                            "type": "string"
                        },
                        "runways": {
                            "type": "array",
                            "items": [
                                {
                                    "type": "object",
                                    "properties": {
                                        "id": {
                                            "type": "string"
                                        },
                                        "dimension": {
                                            "type": "string"
                                        },
                                        "surface": {
                                            "type": "string"
                                        },
                                        "alignment": {
                                            "type": "string"
                                        }
                                    },
                                    "required": [
                                        "id",
                                        "dimension",
                                        "surface",
                                        "alignment"
                                    ]
                                },
                                {
                                    "type": "object",
                                    "properties": {
                                        "id": {
                                            "type": "string"
                                        },
                                        "dimension": {
                                            "type": "string"
                                        },
                                        "surface": {
                                            "type": "string"
                                        },
                                        "alignment": {
                                            "type": "string"
                                        }
                                    },
                                    "required": [
                                        "id",
                                        "dimension",
                                        "surface",
                                        "alignment"
                                    ]
                                }
                            ]
                        },
                        "rwyNum": {
                            "type": "string"
                        },
                        "rwyLength": {
                            "type": "string"
                        },
                        "rwyType": {
                            "type": "string"
                        },
                        "services": {
                            "type": "string"
                        },
                        "tower": {
                            "type": "string"
                        },
                        "beacon": {
                            "type": "string"
                        },
                        "operations": {
                            "type": "string"
                        },
                        "passengers": {
                            "type": "string"
                        },
                        "freqs": {
                            "type": "string"
                        },
                        "priority": {
                            "type": "string"
                        }
                    },
                    "required": [
                        "id",
                        "icaoId",
                        "iataId",
                        "faaId",
                        "name",
                        "state",
                        "country",
                        "source",
                        "type",
                        "lat",
                        "lon",
                        "elev",
                        "magdec",
                        "owner",
                        "runways",
                        "rwyNum",
                        "rwyLength",
                        "rwyType",
                        "services",
                        "tower",
                        "beacon",
                        "operations",
                        "passengers",
                        "freqs",
                        "priority"
                    ]
                }
            ]
}