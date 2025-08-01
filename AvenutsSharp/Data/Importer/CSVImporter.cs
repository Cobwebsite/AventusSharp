using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using CsvHelper;
using CsvHelper.Configuration;

namespace AventusSharp.Data.Importer;

public record CSVImporterConfig<X> : CsvConfiguration
{
    public CSVImporterConfig(CultureInfo cultureInfo) : base(cultureInfo)
    {
    }

    public CSVImporterConfig() : base(CultureInfo.InvariantCulture)
    {
    }

    public Action<CSVMapper<X>>? mapper = null;
    public bool WithId = false;
}
public class CSVImporter
{
    public static VoidWithError Import<X>(string path) where X : IStorable
    {
        return Import<X>(path, new CSVImporterConfig<X>(CultureInfo.InvariantCulture));
    }
    public static VoidWithError Import<X>(string path, CSVImporterConfig<X> config) where X : IStorable
    {
        VoidWithError result = new();
        if (!File.Exists(path))
        {
            result.Errors.Add(new DataError(DataErrorCode.FileNotFound, "The file " + path + " can't be found"));
            return result;
        }

        IGenericDM dm = GenericDM.Get<X>();
        result.Run(() => dm.RunInsideTransaction(() =>
        {
            VoidWithError result = new();
            try
            {
                using (var reader = new StreamReader(path))
                using (var csv = new CsvReader(reader, config))
                {
                    if (config.mapper != null)
                    {
                        CSVMapper<X> csvMapper = new(config.CultureInfo);
                        if (!config.WithId)
                        {
                            csvMapper.Ignore(p => p.Id);
                        }
                        config.mapper(csvMapper);
                        csv.Context.RegisterClassMap(csvMapper.mapper);
                        result.Errors.AddRange(csvMapper.errors);
                    }
                    else if (!config.WithId)
                    {
                        CSVMapper<X> csvMapper = new(config.CultureInfo);
                        csvMapper.Ignore(p => p.Id);
                        csv.Context.RegisterClassMap(csvMapper.mapper);
                    }
                    List<X> records = new();
                    while (csv.Read())
                    {
                        X record = csv.GetRecord<X>();
                        records.Add(record);
                        if (records.Count == config.BufferSize)
                        {
                            result.Run(() => dm.BulkCreateWithError(records, config.WithId));
                            if (!result.Success)
                            {
                                return result;
                            }
                            records.Clear();
                        }
                    }
                    if (records.Count > 0)
                    {
                        result.Run(() => dm.BulkCreateWithError(records, config.WithId));
                    }
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
            return result;
        }));


        return result;
    }
}
