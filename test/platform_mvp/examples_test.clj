(ns platform-mvp.examples-test
  (:require [clojure.edn :as edn]
            [clojure.java.io :as io]
            [clojure.test :refer [deftest is]]
            [platform-mvp.policy :as policy]))

(defn example-files
  []
  (->> (file-seq (io/file "examples"))
       (filter #(and (.isFile %) (.endsWith (.getName %) ".edn")))
       (map #(.getPath %))
       sort))

(deftest all-examples-validate
  (doseq [path (example-files)]
    (is (map? (policy/validate-domain!
               (-> path slurp edn/read-string)))
        (str "valid example: " path))))
