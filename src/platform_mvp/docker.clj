(ns platform-mvp.docker
  (:require [clojure.string :as str]))

(defn run-process
  "Run a process without invoking a shell. Returns {:exit :out :err}."
  [args]
  (let [builder (ProcessBuilder. ^java.util.List (vec args))
        process (.start builder)
        out-future (future (slurp (.getInputStream process)))
        err-future (future (slurp (.getErrorStream process)))
        exit (.waitFor process)]
    {:exit exit
     :out @out-future
     :err @err-future}))

(defn require-success!
  [{:keys [exit out err] :as result} operation]
  (if (zero? exit)
    result
    (throw
     (ex-info (str operation " failed")
              {:operation operation
               :exit exit
               :stdout (str/trim out)
               :stderr (str/trim err)}))))

(defn get-status
  [container-name]
  (-> (run-process ["docker" "inspect"
                    "--format" "{{.State.Status}}"
                    container-name])
      (require-success! "docker inspect")
      :out
      str/trim))

(defn get-logs
  [container-name lines]
  (-> (run-process ["docker" "logs"
                    "--tail" (str lines)
                    container-name])
      (require-success! "docker logs")
      :out
      str/trim))

(defn restart-service
  [container-name]
  (-> (run-process ["docker" "restart" container-name])
      (require-success! "docker restart")
      :out
      str/trim))
