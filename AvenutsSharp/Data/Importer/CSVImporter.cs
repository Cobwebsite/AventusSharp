using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using CsvHelper;
using CsvHelper.Configuration;

namespace AventusSharp.Data.Importer;

public record CSVImporterConfig<X> : CsvConfiguration
{
    public CSVImporterConfig(CultureInfo cultureInfo) : base(cultureInfo)
    {
        BufferSize = 500;
    }

    public CSVImporterConfig() : base(CultureInfo.InvariantCulture)
    {
        BufferSize = 500;
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
        return _import<X>(path, config, false);
    }
    public static VoidWithError BulkImport<X>(string path) where X : IStorable
    {
        return BulkImport<X>(path, new CSVImporterConfig<X>(CultureInfo.InvariantCulture));
    }
    public static VoidWithError BulkImport<X>(string path, CSVImporterConfig<X> config) where X : IStorable
    {
        return _import<X>(path, config, true);
    }
    private static VoidWithError _import<X>(string path, CSVImporterConfig<X> config, bool bulk) where X : IStorable
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
                    else
                    {
                        CSVMapper<X> csvMapper = new(config.CultureInfo);
                        csv.Read();
                        csv.ReadHeader();
                        List<string> names = new();
                        if (csv.HeaderRecord != null)
                        {
                            names.AddRange(typeof(X).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList());
                            names.AddRange(typeof(X).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList());
                            foreach (string header in csv.HeaderRecord)
                            {
                                if (names.Contains(header))
                                {
                                    names.Remove(header);
                                    csvMapper.Map(header, header);
                                }
                            }
                        }
                        if (!config.WithId)
                        {
                            if (!names.Contains("Id"))
                            {
                                csvMapper.Ignore(p => p.Id);
                            }
                        }
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

                    Console.WriteLine(typeof(X).Name + " " + records.Count + " items");
                    if (records.Count > 0)
                    {
                        if (bulk || config.WithId)
                        {
                            result.Run(() => dm.BulkCreateWithError(records, config.WithId));

                        }
                        else
                        {
                            result.Run(() => dm.CreateWithError(records));
                        }
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
